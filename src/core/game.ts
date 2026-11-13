// Spielregeln und Zugablauf. Portiert aus der Paritätsversion (Branch original-paritaet,
// src/Aerger/Game.cs); die seg-Verweise zeigen auf die Routinen in AERGER.EXE.
// Die Zusatzregeln (ab sixBonus) gibt es im Original nicht; ihre Standardwerte
// ergeben exakt das Originalverhalten.

export enum Kind { Human = 0, Computer = 1, Inactive = 2 }

export interface Rules {
  /** Ruhefelder: Figuren darauf können nicht geschlagen werden. */
  rest: boolean;
  /** Schmeißzwang. */
  capture: boolean;
  /** Harter Schmeißzwang: Schlagen vor dem Räumen des Startfelds. */
  hard: boolean;
  /** Absoluter Schmeißzwang: Schlagen vor allem anderen, auch vor dem Raussetzen. */
  absolute: boolean;
  /** Eigene Figuren dürfen nicht übersprungen werden. */
  noJump: boolean;
  /** Mit einer Eins rausgehen. */
  enterOnOne: boolean;
  /** Nach einer Eins nochmal würfeln. */
  repeatOnOne: boolean;
  // ---- Zusatzregeln der neuen Version ----
  /** Nach einer Sechs nochmal würfeln (Original: immer). */
  sixBonus: boolean;
  /** Nach drei Sechsen in Folge ist der Zug zu Ende. */
  threeSixes: boolean;
  /** Raussetzen ist Pflicht, sobald es möglich ist (Original: immer). */
  forcedEntry: boolean;
  /** Das eigene Startfeld muss geräumt werden, solange Figuren im Haus sind (Original: immer). */
  clearStart: boolean;
  /** Das Spiel endet mit dem ersten Sieger statt mit der vollständigen Rangliste. */
  firstWinnerEnds: boolean;
}

export const ORIGINAL_RULES: Readonly<Rules> = Object.freeze({
  rest: false, capture: false, hard: false, absolute: false, noJump: false, enterOnOne: false, repeatOnOne: false,
  sixBonus: true, threeSixes: false, forcedEntry: true, clearStart: true, firstWinnerEnds: false,
});

export function rules(partial: Partial<Rules> = {}): Rules {
  return { ...ORIGINAL_RULES, ...partial };
}

/** Die sieben Regeln, die das Original kennt (Reihenfolge wie im ERG-Format). */
export const ORIGINAL_RULE_KEYS = ['rest', 'capture', 'hard', 'absolute', 'noJump', 'enterOnOne', 'repeatOnOne'] as const;

/** Vollständiger, serialisierbarer Spielzustand (Autosave, Rückgängig). */
export interface GameData {
  playerCount: 4 | 6;
  pieces: number[][];
  rules: Rules;
  kinds: Kind[];
  names: string[];
  player: number;
  die: number;
  awaitingMove: boolean;
  attempts: number;
  winners: number[];
  transitions: number;
  sixesInRow: number;
  pendingNames: string[] | null;
  pendingKinds: Kind[] | null;
  pendingRules: Rules | null;
  pendingBoard: 4 | 6 | null;
  boardChangeRejected: boolean;
}

const range = (n: number) => Array.from({ length: n }, (_, i) => i);

export class Game {
  private pieces: number[][];
  rules: Rules = rules();
  readonly kinds: Kind[] = range(6).map(() => Kind.Human);
  readonly names: string[] = range(6).map(p => `Spieler ${p + 1}`);
  private _playerCount: 4 | 6;
  private _player: number;
  private _die = 0;
  private _awaitingMove = false;
  private _attempts = 0;
  readonly winners: number[] = [];
  /** Anzahl der Spielerwechsel (seg04:0022); die Oberfläche löscht dabei den Würfel. */
  private _transitions = 0;
  private _sixesInRow = 0;

  // Während einer Partie merken Spieler-, Regel- und Brettdialog Änderungen nur vor
  // (seg08:0522..0553, 0f8d..0fbb, 067b..06aa); seg04:0032..0332 übernimmt sie beim nächsten
  // Spielerwechsel in der Reihenfolge Spieler, Brett, Regeln; seg04:15e3..163b am Spielende.
  pendingNames: string[] | null = null;
  pendingKinds: Kind[] | null = null;
  pendingRules: Rules | null = null;
  pendingBoard: 4 | 6 | null = null;
  /** Ein vorgemerkter Wechsel auf das Viererbrett war beim Spielerwechsel nicht möglich (seg07:042c). */
  private _boardChangeRejected = false;

  constructor(players: 4 | 6 = 4, startingPlayer = 0) {
    if (players !== 4 && players !== 6) throw new RangeError('players');
    if (startingPlayer < 0 || startingPlayer >= players) throw new RangeError('startingPlayer');
    this._playerCount = players;
    this._player = startingPlayer;
    this.pieces = range(6).map(() => [-1, -1, -1, -1]);
  }

  get playerCount() { return this._playerCount; }
  get trackLength() { return this._playerCount === 4 ? 40 : 48; }
  get stride() { return this.trackLength / this._playerCount; }
  get player() { return this._player; }
  get die() { return this._die; }
  get awaitingMove() { return this._awaitingMove; }
  get attempts() { return this._attempts; }
  get transitions() { return this._transitions; }
  get sixesInRow() { return this._sixesInRow; }
  get winner() { return this.winners.length === 0 ? -1 : this.winners[0]; }
  get boardChangeRejected() { return this._boardChangeRejected; }
  get hasPendingChanges() { return this.pendingKinds !== null || this.pendingRules !== null || this.pendingBoard !== null; }

  get finished(): boolean {
    if (this.rules.firstWinnerEnds && this.winners.length > 0) return true;
    return range(this._playerCount).every(p => this.kinds[p] === Kind.Inactive || this.winners.includes(p));
  }

  position(player: number, piece: number) { return this.pieces[player][piece]; }
  /** Absolutes Bahnfeld (0-basiert) einer Figur mit der Entfernung distance vom eigenen Start. */
  square(player: number, distance: number) { return (player * this.stride + distance) % this.trackLength; }
  // Ruhefelder DS:1790 (vier) und DS:17b0 (sechs): absolute Felder 5/15/25/35 bzw.
  // 4/12/20/28/36/44, also vier bzw. drei Felder nach jedem Start.
  get restOffset() { return this._playerCount === 4 ? 4 : 3; }
  isRestSquare(square: number) { return square % this.stride === this.restOffset; }

  // seg04:0054..028e entfernt die Figuren deaktivierter Spieler und setzt einen
  // zurückkehrenden Spieler ins Haus. Mensch/Computer-Wechsel behalten die Figuren.
  setPlayerKind(player: number, kind: Kind) {
    if (player < 0 || player >= 6) throw new RangeError('player');
    if (this.kinds[player] !== kind && (this.kinds[player] === Kind.Inactive || kind === Kind.Inactive))
      this.pieces[player] = [-1, -1, -1, -1];
    this.kinds[player] = kind;
  }

  setPendingPlayers(names: string[], kinds: Kind[]) {
    if (names.length !== 6 || kinds.length !== 6) throw new Error('Sechs Spieler erwartet.');
    this.pendingNames = [...names];
    this.pendingKinds = [...kinds];
  }

  takeBoardChangeRejected() {
    const rejected = this._boardChangeRejected;
    this._boardChangeRejected = false;
    return rejected;
  }

  private applyPendingTurnChanges() {
    if (this.pendingKinds) {
      for (let p = 0; p < 6; p++) { this.names[p] = this.pendingNames![p]; this.setPlayerKind(p, this.pendingKinds[p]); }
      this.pendingNames = null; this.pendingKinds = null;
    }
    if (this.pendingBoard !== null) {
      const board = this.pendingBoard;
      this.pendingBoard = null;
      if (!this.changeBoard(board)) this._boardChangeRejected = true;
    }
    if (this.pendingRules) { this.rules = this.pendingRules; this.pendingRules = null; }
  }

  /** seg04:15ae: Am Spielende werden alle Vormerkungen ohne Stellungsprüfung übernommen. Liefert das neue Brett. */
  applyPendingAfterGame(): 4 | 6 | null {
    if (this.pendingRules) this.rules = this.pendingRules;
    if (this.pendingKinds) for (let p = 0; p < 6; p++) { this.names[p] = this.pendingNames![p]; this.kinds[p] = this.pendingKinds[p]; }
    const board = this.pendingBoard;
    this.pendingRules = null; this.pendingNames = null; this.pendingKinds = null; this.pendingBoard = null;
    return board;
  }

  roll(value: number) {
    if (this.finished || this._awaitingMove) throw new Error('Zuerst eine Figur ziehen.');
    if (!Number.isInteger(value) || value < 1 || value > 6) throw new RangeError('value');
    this.ensureActivePlayer();
    this._die = value;
    this._attempts++;
    this._awaitingMove = true;
    this._sixesInRow = value === 6 ? this._sixesInRow + 1 : 0;
  }

  target(piece: number) {
    const at = this.pieces[this._player][piece];
    return at < 0 ? 0 : at + this._die;
  }

  isCapture(piece: number) {
    const to = this.target(piece);
    return to < this.trackLength && this.enemyAt(this.square(this._player, to));
  }

  /** Spieler und Figur, die beim Zug von piece geschlagen würden, oder null. */
  victim(piece: number): [number, number] | null {
    const to = this.target(piece);
    if (to >= this.trackLength) return null;
    const sq = this.square(this._player, to);
    for (let p = 0; p < this._playerCount; p++) {
      if (p === this._player || this.kinds[p] === Kind.Inactive) continue;
      for (let i = 0; i < 4; i++) {
        const at = this.pieces[p][i];
        if (at >= 0 && at < this.trackLength && this.square(p, at) === sq) return [p, i];
      }
    }
    return null;
  }

  private enemyAt(square: number) {
    for (let p = 0; p < this._playerCount; p++) {
      if (p === this._player || this.kinds[p] === Kind.Inactive) continue;
      for (let i = 0; i < 4; i++) {
        const at = this.pieces[p][i];
        if (at >= 0 && at < this.trackLength && this.square(p, at) === square) return true;
      }
    }
    return false;
  }

  private canMove(piece: number) {
    const own = this.pieces[this._player];
    const from = own[piece];
    if (from < 0 && this._die !== 6 && !(this._die === 1 && this.rules.enterOnOne)) return false;
    const to = this.target(piece);
    if (to >= this.trackLength + 4) return false;
    for (let j = 0; j < 4; j++) {
      if (j === piece) continue;
      const other = own[j];
      if (other === to) return false;
      if (other >= 0 && other > from && other < to && (this.rules.noJump || other >= this.trackLength)) return false;
    }
    return !(this.rules.rest && to < this.trackLength && this.isRestSquare(this.square(this._player, to)) && this.enemyAt(this.square(this._player, to)));
  }

  legalMoves(): number[] {
    if (!this._awaitingMove || this.finished) return [];
    const own = this.pieces[this._player];
    const r = this.rules;
    const all = range(4).filter(i => this.canMove(i));
    const captures = all.filter(i => this.isCapture(i));
    if (r.capture && r.absolute && captures.length > 0) return captures;
    const enter = all.filter(i => own[i] < 0);
    // seg13:0dc7: schlägt schon das Raussetzen auf dem Startfeld, darf beim Schmeißzwang
    // ein beliebiger Schlagzug gewählt werden ("eine beliebige aussuchen").
    if (enter.length > 0 && r.forcedEntry) return r.capture && captures.some(i => enter.includes(i)) ? captures : enter;
    if (r.capture && r.hard && captures.length > 0) return captures;
    if (r.clearStart && own.some(at => at < 0)) {
      let at = own.indexOf(0);
      if (at >= 0) {
        const visited = new Set<number>();
        while (!visited.has(at)) {
          visited.add(at);
          if (all.includes(at)) return [at];
          const to = own[at] + this._die;
          const blocker = range(4).find(i => i !== at && own[i] === to);
          if (blocker === undefined) return [];
          at = blocker;
        }
        return [];
      }
    }
    if (r.capture && captures.length > 0) return captures;
    return all;
  }

  /** Zieht eine Figur; liefert true, wenn dabei geschlagen wurde. */
  move(piece: number): boolean {
    if (!this.legalMoves().includes(piece)) throw new Error('Dieser Zug ist nicht erlaubt.');
    const to = this.target(piece);
    let captured = false;
    if (to < this.trackLength) {
      const sq = this.square(this._player, to);
      for (let p = 0; p < this._playerCount; p++)
        for (let i = 0; i < 4; i++)
          if (p !== this._player && this.kinds[p] !== Kind.Inactive && this.pieces[p][i] >= 0 &&
              this.pieces[p][i] < this.trackLength && this.square(p, this.pieces[p][i]) === sq) {
            this.pieces[p][i] = -1; captured = true;
          }
    }
    this.pieces[this._player][piece] = to;
    if (this.pieces[this._player].every(at => at >= this.trackLength)) {
      this.winners.push(this._player);
      const remaining = range(this._playerCount).filter(p => this.kinds[p] !== Kind.Inactive && !this.winners.includes(p));
      // seg04:0a6c..0ad9 gibt dem einzigen verbleibenden Spieler den letzten Platz.
      if (remaining.length === 1 && !this.rules.firstWinnerEnds) this.winners.push(remaining[0]);
    }
    this.endTurn(false);
    return captured;
  }

  /** seg04:06f1..079e: drei Startversuche nur, wenn alle Figuren außerhalb des Hauses
   *  lückenlos die hintersten Zielfelder belegen. */
  get entryAttempts(): boolean {
    const own = this.pieces[this._player];
    const outsideYard = own.filter(at => at >= 0).length;
    for (let goal = this.trackLength + 4 - outsideYard; goal < this.trackLength + 4; goal++)
      if (!own.includes(goal)) return false;
    return outsideYard < 4;
  }

  pass() {
    if (!this._awaitingMove || this.legalMoves().length !== 0) throw new Error('Ein Zug ist möglich.');
    const entryAttempts = this.entryAttempts;
    // seg04:07f0..080c beendet den dritten erfolglosen Startversuch vor der späteren
    // Prüfung "Nochmal bei 1"; nur "Raussetzen mit 1" nimmt eine Eins aus.
    const exhausted = entryAttempts && this._attempts >= 3 && this._die !== 6 && !(this._die === 1 && this.rules.enterOnOne);
    this.endTurn(entryAttempts && this._attempts < 3, !exhausted);
  }

  ensureActivePlayer() {
    if (this.finished) { this._awaitingMove = false; this._attempts = 0; return; }
    for (let n = 0; n < this._playerCount && (this.kinds[this._player] === Kind.Inactive || this.winners.includes(this._player)); n++) {
      this._player = (this._player + 1) % this._playerCount;
      this._attempts = 0; this._awaitingMove = false; this._sixesInRow = 0;
    }
  }

  reconcileTurn() {
    this.ensureActivePlayer();
    if (this._awaitingMove && this.legalMoves().length === 0) this.pass();
  }

  // seg07:042c/0584: absolute Bahnfelder 1..40 bleiben; Spieler 5/6 beginnen im Haus.
  changeBoard(players: 4 | 6): boolean {
    if (players !== 4 && players !== 6) throw new RangeError('players');
    if (players === this._playerCount) return true;
    if (players === 4) {
      if (range(4).every(p => this.kinds[p] === Kind.Inactive)) return false;
      for (let p = 0; p < 4; p++)
        for (let i = 0; i < 4; i++) {
          const at = this.pieces[p][i];
          if (at >= 0 && at < this.trackLength && this.square(p, at) >= 40) return false;
        }
    }
    const oldLength = this.trackLength, newLength = players === 4 ? 40 : 48;
    for (let p = 0; p < 4; p++)
      for (let i = 0; i < 4; i++) {
        const at = this.pieces[p][i];
        if (at >= oldLength) this.pieces[p][i] = newLength + at - oldLength;
        else if (at >= 0) this.pieces[p][i] = (this.square(p, at) - p * (newLength / players) + newLength) % newLength;
      }
    for (let p = 4; p < 6; p++) this.pieces[p] = [-1, -1, -1, -1];
    for (let k = this.winners.length - 1; k >= 0; k--) if (this.winners[k] >= 4) this.winners.splice(k, 1);
    this._playerCount = players;
    if (this._player >= players) { this._player = 0; this._awaitingMove = false; this._attempts = 0; this._sixesInRow = 0; }
    this.ensureActivePlayer();
    return true;
  }

  private endTurn(retry: boolean, allowBonus = true) {
    this._awaitingMove = false;
    const r = this.rules;
    let bonus = allowBonus && ((this._die === 6 && r.sixBonus) || (this._die === 1 && r.repeatOnOne));
    if (bonus && this._die === 6 && r.threeSixes && this._sixesInRow >= 3) bonus = false;
    if (!bonus && !retry) {
      // seg04:0022: zuerst Vormerkungen übernehmen, dann zum nächsten Spieler
      // (oberhalb der eventuell neuen Spielerzahl auf Spieler 1 umbrechen).
      const current = this._player;
      this._transitions++;
      this.applyPendingTurnChanges();
      this._player = current + 1 >= this._playerCount ? 0 : current + 1;
      this._attempts = 0;
      this._sixesInRow = 0;
    } else if (!retry) this._attempts = 0;
    this.ensureActivePlayer();
  }

  // ---- Zugriff für Laden, Tests und Rückgängig ----
  setPosition(player: number, piece: number, position: number) { this.pieces[player][piece] = position; }
  restoreTurn(player: number, die: number, awaitingMove: boolean, attempts: number) {
    this._player = player; this._die = die; this._awaitingMove = awaitingMove; this._attempts = attempts;
  }

  snapshot(): GameData {
    return structuredClone({
      playerCount: this._playerCount, pieces: this.pieces, rules: this.rules, kinds: this.kinds, names: this.names,
      player: this._player, die: this._die, awaitingMove: this._awaitingMove, attempts: this._attempts,
      winners: this.winners, transitions: this._transitions, sixesInRow: this._sixesInRow,
      pendingNames: this.pendingNames, pendingKinds: this.pendingKinds, pendingRules: this.pendingRules,
      pendingBoard: this.pendingBoard, boardChangeRejected: this._boardChangeRejected,
    });
  }

  static restore(data: GameData): Game {
    const d = structuredClone(data);
    const g = new Game(d.playerCount, d.player);
    g.pieces = d.pieces;
    g.rules = rules(d.rules);
    for (let p = 0; p < 6; p++) { g.kinds[p] = d.kinds[p]; g.names[p] = d.names[p]; }
    g._die = d.die; g._awaitingMove = d.awaitingMove; g._attempts = d.attempts;
    g.winners.push(...d.winners);
    g._transitions = d.transitions; g._sixesInRow = d.sixesInRow ?? 0;
    g.pendingNames = d.pendingNames; g.pendingKinds = d.pendingKinds;
    g.pendingRules = d.pendingRules ? rules(d.pendingRules) : null;
    g.pendingBoard = d.pendingBoard; g._boardChangeRejected = d.boardChangeRejected;
    return g;
  }

  clone(): Game { return Game.restore(this.snapshot()); }
}

/** true, wenn nur Regeln des Originals verwendet werden (dann passt der Stand ins ERG-Format). */
export function usesOnlyOriginalRules(r: Rules): boolean {
  return (Object.keys(ORIGINAL_RULES) as (keyof Rules)[])
    .every(k => (ORIGINAL_RULE_KEYS as readonly string[]).includes(k) || r[k] === ORIGINAL_RULES[k]);
}
