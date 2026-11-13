// Spielablauf der Oberfläche: Würfel- und Zuganimation, Computerzüge, Rückgängig, Autosave,
// Statistik und Klänge. Zeiten aus dem Original (DS:0124, DS:010c, seg05); anders als dort
// wartet der Ablauf nicht auf das Ende eines Klangs.
import { readErg, writeErg } from '../core/erg';
import { Game, Kind, type GameData, type Rules } from '../core/game';
import { OriginalRandom } from '../core/random';
import { chooseMove } from '../core/strategy';
import { SOUND, SoundPlayer } from './sound';
import {
  emptyStats, loadGame, loadSettings, loadSlots, loadStats, saveGame, saveSettings, saveSlots, saveStats,
  type PlayerSetup, type SaveSlot, type Settings, type Speed, type Stats,
} from './storage';

const ROLL_FRAME = [65, 25, 10, 0];          // seg04:051c, 18 Bilder
const STEP = [500, 200, 100, 0];             // seg05:0022
// Pausen vor Computerschritten. Länger als im Original (DS:010c: 2000/600/200, 1000/500/200,
// 1200/600/100), damit man den Zügen folgen kann.
const DELAY_MOVE = [2000, 1000, 400, 0];     // Computer zieht
const DELAY_RETRY = [1200, 800, 300, 0];     // Computer würfelt erneut
const DELAY_ROLL = [1600, 1200, 500, 0];     // Computer würfelt (nach dem Spielerwechsel)
/** Rückgängig reicht bis zum Spielanfang; die Grenze schützt nur den Browser-Speicher. */
const UNDO_LIMIT = 2000;
const AUTO_SLOTS = 5;
/** Flugzeit einer geschlagenen Figur zurück ins Haus. */
export const FLY_MS = [1000, 800, 550, 0];

export type Notice = 'changes' | 'boardRejected' | 'noPlayers' | null;

export interface View {
  /** Laufende Partie (oder die zuletzt beendete, solange der Titel nicht gezeigt wird). */
  game: Game | null;
  playing: boolean;
  shownDie: number;
  rolling: boolean;
  /** Figur in Bewegung, mit ihrer aktuellen Zwischenposition. */
  moving: { player: number; piece: number; position: number } | null;
  /** Geschlagene Figur, die bis zur Ankunft des Angreifers traurig auf ihrem Feld steht. */
  victim: { player: number; piece: number } | null;
  /** Mit Tab gewählte Figur (schwarzer Rahmen wie im Original). */
  selected: number;
  toast: string | null;
  /** Geschlagene Figur auf dem Rückflug ins Haus. */
  flying: { player: number; piece: number } | null;
  winnersOpen: boolean;
  /** Rangliste von Hand geöffnet (mitten im Spiel, ohne neuen Sieger). */
  winnersManual: boolean;
  notice: Notice;
}

export class Controller {
  settings: Settings = loadSettings();
  stats: Stats = loadStats();
  readonly sound = new SoundPlayer(() => this.settings);
  view: View = { game: null, playing: false, shownDie: 0, rolling: false, moving: null, victim: null, selected: -1, toast: null, flying: null, winnersOpen: false, winnersManual: false, notice: null };
  private undoStack: GameData[] = [];
  private random = new OriginalRandom(OriginalRandom.seedFromClock(new Date()));
  private listeners = new Set<() => void>();
  private generation = 0;
  private busy = false;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | null = null;
  private flyTimer: ReturnType<typeof setTimeout> | null = null;
  private toastPlayer = -1;
  private repeatRoll = false;
  private announcedWinners = 0;
  /** Solange ein Dialog offen ist, wartet der Computer. */
  private modal = false;

  constructor() {
    const saved = loadGame();
    if (saved) {
      try {
        const game = Game.restore(saved.game);
        if (!game.finished) {
          this.view.game = game;
          this.view.playing = true;
          this.view.shownDie = saved.shownDie;
          this.undoStack = saved.undo ?? [];
          this.announcedWinners = game.winners.length;
        }
      } catch { saveGame(null); }
    }
  }

  // ---------- Beobachtung ----------
  subscribe(listener: () => void) { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; }
  private emit() { this.view = { ...this.view }; for (const l of this.listeners) l(); }

  get game() { return this.view.game; }
  /** Rückgängig geht jederzeit Schritt für Schritt zurück bis zum Spielanfang. Haltepunkte sind
   *  „vor dem Zug“ (Wurf bleibt stehen) und „vor dem Wurf“ eines Menschen: erstes Zurück nimmt den
   *  Figurenzug zurück (samt Computerzügen danach), das zweite auch den Wurf. */
  get canUndo() {
    return this.settings.allowUndo && this.view.playing && !!this.view.game && this.undoStack.length > 0;
  }
  /** Klänge: Menschen hören immer alles (auch im Turbo); Computer im Turbo bleiben still,
   *  sonst überlagern sich Dutzende Klänge pro Sekunde. Stufen wie im Original (seg04). */
  private soundSpeed(player: number, speed: Speed): Speed {
    return this.view.game?.kinds[player] === Kind.Human ? (Math.min(speed, 1) as Speed) : speed;
  }
  get humanTurn() {
    const g = this.view.game;
    return !!g && this.view.playing && !g.finished && g.kinds[g.player] === Kind.Human;
  }
  /** Züge, die der Mensch gerade machen darf (für Hervorhebung und Klicks). */
  get humanMoves(): number[] {
    const g = this.view.game;
    if (!g || !this.humanTurn || this.busy || !g.awaitingMove) return [];
    return g.legalMoves();
  }
  speedOf(player: number): Speed {
    const g = this.view.game;
    if (g?.kinds[player] !== Kind.Computer) return this.settings.speedHuman;
    return this.settings.fastWithoutHumans && this.humansDone ? 3 : this.settings.speedComputer;
  }
  /** Es haben Menschen mitgespielt, aber alle sind schon im Ziel. */
  get humansDone() {
    const g = this.view.game;
    if (!g) return false;
    const humans = g.kinds.slice(0, g.playerCount).map((k, p) => k === Kind.Human ? p : -1).filter(p => p >= 0);
    return humans.length > 0 && humans.every(p => g.winners.includes(p));
  }

  setModal(open: boolean) {
    this.modal = open;
    if (!open) this.schedule();
    else this.clearTimer();
  }

  updateSettings(change: Partial<Settings>) {
    this.settings = { ...this.settings, ...change };
    saveSettings(this.settings);
    this.sound.applyVolume();
    this.emit();
  }

  // ---------- Partie ----------
  newGame() {
    const s = this.settings;
    const active = s.players.slice(0, s.board).map((p, i) => (p.kind !== Kind.Inactive ? i : -1)).filter(i => i >= 0);
    if (active.length === 0) { this.view.notice = 'noPlayers'; this.emit(); return false; }
    this.archiveCurrent();
    this.cancel();
    // seg02:0d3d zieht Random(Spielerzahl) für den Beginner; wie in der Paritätsversion beginnt
    // der erste aktive Spieler, der Generator bleibt aber im Takt.
    this.random.next(s.board);
    const game = new Game(s.board, active[0]);
    game.rules = { ...s.rules };
    s.players.forEach((p, i) => { game.names[i] = p.name; game.kinds[i] = p.kind; });
    this.view = { ...this.view, game, playing: true, shownDie: 0, moving: null, victim: null, flying: null, selected: -1, winnersOpen: false };
    this.undoStack = [];
    this.announcedWinners = 0;
    this.repeatRoll = false;
    this.persist();
    this.emit();
    this.schedule();
    return true;
  }

  /** Spiel beenden (seg04:15ae übernimmt Vormerkungen). */
  endGame() {
    const g = this.view.game;
    if (g && !g.finished) this.archiveCurrent();
    this.cancel();
    if (g) {
      const board = g.applyPendingAfterGame();
      if (board) this.updateSettings({ board });
    }
    this.view = { ...this.view, playing: false, game: null, shownDie: 0, moving: null, victim: null, selected: -1 };
    this.undoStack = [];
    saveGame(null);
    this.emit();
  }

  // ---------- Eingaben ----------
  /** Klick/Taste zum Würfeln. */
  rollRequest() {
    if (!this.humanTurn || this.busy || this.modal || this.view.game!.awaitingMove) return;
    void this.roll();
  }

  /** Klick auf eine Figur des aktuellen Spielers. */
  moveRequest(piece: number) {
    if (!this.humanTurn || this.busy || this.modal) return;
    if (!this.view.game!.legalMoves().includes(piece)) return;
    void this.move(piece);
  }

  /** Tab / Enter / Leertaste wie im Original (seg09:0b65, seg04:0412). */
  keyRequest(key: 'tab' | 'enter' | 'shift-tab') {
    const g = this.view.game;
    if (!g || !this.humanTurn || this.busy || this.modal) return;
    if (!g.awaitingMove) { if (key === 'enter') this.rollRequest(); return; }
    const moves = g.legalMoves();
    if (moves.length === 0) return;
    const next = (start: number, step: number) => {
      for (let k = 0; k < 4; k++) { const p = (start + step * k + 8) % 4; if (moves.includes(p)) return p; }
      return moves[0];
    };
    const sel = this.view.selected;
    if (sel < 0) this.view.selected = next(0, 1);
    else if (key === 'tab') this.view.selected = next(sel + 1, 1);
    else if (key === 'shift-tab') this.view.selected = next(sel - 1, -1);
    else if (moves.includes(sel)) { void this.move(sel); return; }
    this.emit();
  }

  undo() {
    if (!this.canUndo) return;
    this.cancel();
    const data = this.undoStack.pop()!;
    const game = Game.restore(data);
    // Der Stand vor dem Wurf: der Würfel ist leer (nach Fehlversuchen steht er wie im Original da).
    this.view = { ...this.view, game, shownDie: game.awaitingMove || game.attempts > 0 ? game.die : 0, moving: null, victim: null, flying: null, selected: -1, winnersOpen: false, winnersManual: false };
    this.announcedWinners = game.winners.length;
    this.persist();
    this.emit();
    this.schedule();
  }

  // ---------- Änderungen während der Partie ----------
  /** Spieler übernehmen; während einer Partie erst beim nächsten Spielerwechsel (seg08). */
  setPlayers(players: PlayerSetup[]) {
    const kinds = players.map(p => p.kind);
    const board = this.view.game?.playerCount ?? this.settings.board;
    // seg08:04c8..051d: ohne aktiven Spieler auf dem Brett wird Spieler 1 Mensch.
    if (!kinds.slice(0, board).some(k => k !== Kind.Inactive)) { players = players.map((p, i) => i === 0 ? { ...p, kind: Kind.Human } : p); }
    this.updateSettings({ players });
    const g = this.view.game;
    if (g && this.view.playing) {
      const changed = players.some((p, i) => p.name !== g.names[i] || p.kind !== g.kinds[i]);
      if (changed) { g.setPendingPlayers(players.map(p => p.name), players.map(p => p.kind)); this.notify('changes'); }
      this.persist();
    }
    this.schedule();
  }

  setRules(r: Rules) {
    this.updateSettings({ rules: r });
    const g = this.view.game;
    if (g && this.view.playing && JSON.stringify(g.rules) !== JSON.stringify(r)) { g.pendingRules = { ...r }; this.notify('changes'); this.persist(); }
  }

  setBoard(board: 4 | 6) {
    this.updateSettings({ board });
    const g = this.view.game;
    if (g && this.view.playing && g.playerCount !== board) { g.pendingBoard = board; this.notify('changes'); this.persist(); }
  }

  closeNotice() { this.view.notice = null; this.emit(); }
  closeWinners(endGame: boolean) {
    this.view.winnersOpen = false;
    if (endGame || this.view.game?.finished) this.endGame(); else this.emit();
    this.schedule();
  }
  openWinners(manual = false) { this.view.winnersOpen = true; this.view.winnersManual = manual; this.emit(); }

  // ---------- Spielstand-Dateien ----------
  exportErg(): Uint8Array | null { return this.view.game && this.view.playing ? writeErg(this.view.game) : null; }
  exportJson(): string | null {
    return this.view.game && this.view.playing ? JSON.stringify({ format: 'aerger-2', game: this.view.game.snapshot() }) : null;
  }
  // ---------- Spielstände in der App ----------
  slots(): SaveSlot[] { return loadSlots(); }
  saveSlot(name: string) {
    const g = this.view.game;
    if (!g || !this.view.playing) return;
    const slot: SaveSlot = { id: `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`, name: name.trim() || 'Spielstand', saved: new Date().toISOString(), game: g.snapshot() };
    saveSlots([slot, ...loadSlots()]);
    this.showToast('Gespeichert');
  }
  deleteSlot(id: string) { saveSlots(loadSlots().filter(s => s.id !== id)); this.emit(); }
  loadSlot(id: string) {
    const slot = loadSlots().find(s => s.id === id);
    if (slot) this.startLoaded(Game.restore(slot.game));
  }

  importFile(bytes: Uint8Array) {
    let game: Game;
    if (bytes[0] === 0x7b) {   // '{' – eigenes JSON-Format mit allen Zusatzregeln
      const data = JSON.parse(new TextDecoder().decode(bytes));
      if (data?.format !== 'aerger-2') throw new Error('Unbekanntes Format.');
      game = Game.restore(data.game);
    } else game = readErg(bytes);
    this.startLoaded(game);
  }

  private pushUndo(g: Game) {
    this.undoStack.push(g.snapshot());
    if (this.undoStack.length > UNDO_LIMIT) this.undoStack.shift();
  }

  /** Sichert die laufende Partie als automatischen Spielstand, bevor sie ersetzt wird
   *  (neues Spiel, Laden, Beenden) – wie „Weiterspielen“ in Videospielen. */
  private archiveCurrent() {
    const g = this.view.game;
    if (!g || !this.view.playing || (this.undoStack.length === 0 && g.transitions === 0)) return;
    const data = g.snapshot();
    const slots = loadSlots();
    if (slots.some(s => s.auto && JSON.stringify(s.game) === JSON.stringify(data))) return;
    const names = g.kinds.slice(0, g.playerCount).map((k, p) => k === Kind.Human ? g.names[p] : null).filter(Boolean).join(', ');
    const auto: SaveSlot = { id: `a${Date.now().toString(36)}`, name: `Automatisch gesichert${names ? ` – ${names}` : ''}`, saved: new Date().toISOString(), game: data, auto: true };
    const autos = [auto, ...slots.filter(s => s.auto)].slice(0, AUTO_SLOTS);
    saveSlots([...autos, ...slots.filter(s => !s.auto)]);
  }

  private startLoaded(game: Game) {
    this.archiveCurrent();
    // seg12:05b5 verwirft beim Laden vorgemerkte Spieleränderungen; Regel- und Brettwechsel bleiben.
    const old = this.view.game;
    if (old && this.view.playing) { game.pendingRules ??= old.pendingRules; game.pendingBoard ??= old.pendingBoard; }
    this.cancel();
    game.reconcileTurn();
    // seg09:0202: nach dem Laden steht der Würfel nur bei offenem Zug oder Fehlversuchen da.
    this.view = { ...this.view, game, playing: true, shownDie: game.awaitingMove || game.attempts > 0 ? game.die : 0, moving: null, victim: null, flying: null, selected: -1, winnersOpen: false };
    this.undoStack = [];
    this.announcedWinners = game.winners.length;
    this.persist();
    this.emit();
    this.schedule();
  }

  resetStats() { this.stats = {}; saveStats(this.stats); this.emit(); }

  // ---------- intern ----------
  private notify(notice: Notice) { this.view.notice = notice; this.emit(); }

  showToast(text: string, ms = 1600) {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.view.toast = text;
    this.toastPlayer = this.view.game?.player ?? -1;
    this.emit();
    this.toastTimer = setTimeout(() => { this.view.toast = null; this.emit(); }, ms);
  }

  private persist() {
    const g = this.view.game;
    if (!g || !this.view.playing) { saveGame(null); return; }
    saveGame({ game: g.snapshot(), undo: this.undoStack.slice(-UNDO_LIMIT), shownDie: this.view.shownDie });
  }

  private clearTimer() { if (this.timer) { clearTimeout(this.timer); this.timer = null; } }

  /** Bricht laufende Animationen und Computerzüge ab. */
  private cancel() { this.generation++; this.busy = false; this.clearTimer(); this.view.rolling = false; }

  private sleep(ms: number, token: number): Promise<boolean> {
    if (ms <= 0) return Promise.resolve(token === this.generation);
    return new Promise(resolve => setTimeout(() => resolve(token === this.generation), ms));
  }

  private stat(player: number): import('./storage').PlayerStats {
    const name = this.view.game!.names[player].trim() || `Spieler ${player + 1}`;
    return (this.stats[name] ??= emptyStats());
  }

  /** Plant den nächsten Computerschritt bzw. prüft den Zugzustand. */
  schedule() {
    this.clearTimer();
    const g = this.view.game;
    if (!g || !this.view.playing || this.busy || this.modal || this.view.winnersOpen) return;
    if (g.takeBoardChangeRejected()) { this.notify('boardRejected'); return; }
    // Ein beim Spielerwechsel übernommener Wechsel kann alle Spieler entfernen; dann ist Schluss.
    if (g.finished) {
      if (g.winners.length > this.announcedWinners || g.winners.length === 0) { this.announcedWinners = g.winners.length; this.openWinners(); }
      return;
    }
    const before = g.player, pending = g.awaitingMove;
    g.reconcileTurn();
    if (pending && !g.awaitingMove) { this.repeatRoll = g.player === before; this.emit(); }
    if (g.kinds[g.player] !== Kind.Computer) return;
    const speed = this.speedOf(g.player);
    const delay = g.awaitingMove ? DELAY_MOVE[speed] : this.repeatRoll ? DELAY_RETRY[speed] : DELAY_ROLL[speed];
    this.timer = setTimeout(() => {
      this.timer = null;
      if (this.busy || this.modal || !this.view.playing) return;
      if (!g.awaitingMove) void this.roll();
      else {
        const moves = g.legalMoves();
        if (moves.length === 0) { g.pass(); this.afterTurn(); }
        else void this.move(chooseMove(g, this.settings.players[g.player].strength));
      }
    }, Math.max(1, delay));
  }

  private async roll() {
    const g = this.view.game!;
    const token = ++this.generation;
    this.busy = true;
    const player = g.player, speed = this.speedOf(player), loud = this.soundSpeed(player, speed);
    if (g.kinds[player] === Kind.Human) this.pushUndo(g);
    this.view.selected = -1;
    // Ein Hinweis des vorigen Spielers verschwindet, sobald der nächste würfelt.
    if (this.view.toast && this.toastPlayer !== player) { if (this.toastTimer) clearTimeout(this.toastTimer); this.view.toast = null; }
    if (loud < 2) this.sound.play(SOUND.roll);
    this.view.rolling = true;
    for (let frame = 0; frame < 18 && ROLL_FRAME[speed] > 0; frame++) {
      // seg05:00b6..0122 wechselt die Augenzahl in den Bildern 1..10 und hält sie dann.
      if (frame < 10) { this.view.shownDie = this.random.next(6) + 1; this.emit(); }
      if (!(await this.sleep(ROLL_FRAME[speed], token))) return;
    }
    if (ROLL_FRAME[speed] === 0) this.view.shownDie = this.random.next(6) + 1;
    this.view.rolling = false;
    const die = this.view.shownDie;
    const entry = g.entryAttempts;
    g.roll(die);
    const st = this.stat(player);
    st.rolls++; if (die === 6) st.sixes++;
    saveStats(this.stats);
    if (loud < 2) this.sound.play(8 + die);
    this.busy = false;
    const moves = g.legalMoves();
    if (moves.length === 0) {
      const attempt = g.attempts;
      const retry = entry && attempt < 3 && die !== 6;
      if (g.kinds[player] === Kind.Human) this.showToast(retry ? 'Nochmal würfeln' : 'Kein Zug möglich');
      const transitions = g.transitions;
      g.pass();
      // seg04:0022 löscht den Würfel beim Spielerwechsel; Wiederholung lässt ihn stehen.
      if (g.transitions !== transitions) this.view.shownDie = 0;
      this.repeatRoll = !g.awaitingMove && g.player === player;
      this.afterTurn();
      return;
    }
    this.repeatRoll = false;
    this.persist();
    this.emit();
    if (g.kinds[player] === Kind.Human && this.settings.autoMove && moves.length === 1) {
      if (await this.sleep(250, token)) void this.move(moves[0]);
      return;
    }
    this.schedule();
  }

  private async move(piece: number) {
    const g = this.view.game!;
    const token = ++this.generation;
    this.clearTimer();
    this.busy = true;
    const player = g.player, speed = this.speedOf(player), loud = this.soundSpeed(player, speed);
    const from = g.position(player, piece), to = g.target(piece);
    const victim = g.victim(piece);
    if (g.kinds[player] === Kind.Human) this.pushUndo(g);
    this.view.selected = -1;
    this.view.moving = { player, piece, position: from };
    if (victim) {
      this.view.victim = { player: victim[0], piece: victim[1] };
      // seg04:12f6/0d5a: Schlagton bei Tempo bis "schnell" (Bahn) bzw. "normal" (Raussetzen).
      if (loud < (from < 0 ? 2 : 3)) this.sound.play(SOUND.capture);
    } else if (from < 0 && loud < 3) this.sound.play(SOUND.enter);
    this.emit();
    if (from < 0) {
      // Raussetzen ohne Schritte (seg04:0f64).
      this.view.moving = { player, piece, position: to };
      this.emit();
      if (!(await this.sleep(STEP[speed], token))) return;
    } else {
      for (let at = from + 1; at <= to; at++) {
        this.view.moving = { player, piece, position: at };
        // Ohne Schrittpause (Turbo) nur ein Schrittklang statt vieler gleichzeitiger.
        if (loud < 3 && (STEP[speed] > 0 || at === to)) this.sound.play(SOUND.step);
        this.emit();
        if (!(await this.sleep(STEP[speed], token))) return;
      }
    }
    if (victim && loud < 2) this.sound.play(SOUND.home);
    const captured = g.move(piece);
    if (captured && victim && FLY_MS[speed] > 0) {
      // Die geschlagene Figur fliegt sichtbar zurück ins Haus (Stage: Klasse "flying").
      if (this.flyTimer) clearTimeout(this.flyTimer);
      this.view.flying = { player: victim[0], piece: victim[1] };
      this.flyTimer = setTimeout(() => { this.view.flying = null; this.flyTimer = null; this.emit(); }, FLY_MS[speed]);
    }
    if (captured && victim) {
      this.stat(player).captures++;
      this.stat(victim[0]).captured++;
      saveStats(this.stats);
    }
    this.view.moving = null;
    this.view.victim = null;
    this.view.shownDie = 0;   // seg04:0b3e löscht den Würfel nach jedem Zug
    this.busy = false;
    this.repeatRoll = false;
    this.afterTurn();
  }

  private afterTurn() {
    const g = this.view.game!;
    if (g.winners.length > this.announcedWinners) {
      this.announcedWinners = g.winners.length;
      this.sound.play(SOUND.winner);
      if (g.finished) this.recordResult(g);
      this.persist();
      this.openWinners();
      return;
    }
    this.persist();
    this.emit();
    this.schedule();
  }

  private recordResult(g: Game) {
    for (let p = 0; p < g.playerCount; p++) {
      if (g.kinds[p] === Kind.Inactive) continue;
      const st = this.stat(p);
      st.games++;
      const place = g.winners.indexOf(p);
      if (place === 0) st.wins++;
      if (place >= 0) st.places[place]++;
    }
    saveStats(this.stats);
  }
}
