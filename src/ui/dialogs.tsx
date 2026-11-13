// Dialoge nach den Originalressourcen (DLG_*), ergänzt um die neuen Einstellungen.
import { useState } from 'preact/hooks';
import { Kind, ORIGINAL_RULES, type Rules } from '../core/game';
import type { Strength } from '../core/strategy';
import { Button, Check, Dialog, Group, Radio, Slider } from './bwcc';
import type { Controller } from './controller';
import { SOUND } from './sound';
import type { PlayerSetup, SaveSlot, Settings, Speed } from './storage';
import { PLAYERS } from './theme';

interface DialogProps { controller: Controller; close: () => void }

export function PlayersDialog({ controller, close }: DialogProps) {
  const g = controller.view.game;
  const pending = g?.pendingKinds && g.pendingNames;
  const [players, setPlayers] = useState<PlayerSetup[]>(() => controller.settings.players.map((p, i) => ({
    ...p,
    name: pending ? g!.pendingNames![i] : g && controller.view.playing ? g.names[i] : p.name,
    kind: pending ? g!.pendingKinds![i] : g && controller.view.playing ? g.kinds[i] : p.kind,
  })));
  const update = (i: number, change: Partial<PlayerSetup>) => setPlayers(list => list.map((p, k) => k === i ? { ...p, ...change } : p));
  const ok = () => { controller.setPlayers(players.map(p => ({ ...p, name: p.name.slice(0, 21) }))); close(); };
  return (
    <Dialog title="Spieler" onCancel={close} class="players" buttons={<><Button kind="ok" isDefault onClick={ok} /><Button kind="cancel" onClick={close} /></>}>
      <div class="player-grid">
        {players.map((p, i) => (
          <Group key={i} class="player" title={<>Spieler {i + 1} <span class="dim">({PLAYERS[i].name})</span></>}>
            <label class="bw-field"><span>Name</span><input type="text" maxLength={21} value={p.name} onInput={e => update(i, { name: (e.currentTarget as HTMLInputElement).value })} /></label>
            <hr />
            <Radio name={`kind${i}`} checked={p.kind === Kind.Human} onChange={() => update(i, { kind: Kind.Human })}>Mensch</Radio>
            <Radio name={`kind${i}`} checked={p.kind === Kind.Computer} onChange={() => update(i, { kind: Kind.Computer })}>Computer</Radio>
            <div class={`strength${p.kind === Kind.Computer ? '' : ' hidden'}`}>
              {(['easy', 'normal', 'hard'] as Strength[]).map(s => (
                <button type="button" key={s} class={`chip${p.strength === s ? ' on' : ''}`} aria-pressed={p.strength === s}
                  disabled={p.kind !== Kind.Computer} onClick={() => update(i, { strength: s })}>{{ easy: 'leicht', normal: 'normal', hard: 'schwer' }[s]}</button>
              ))}
            </div>
            <Radio name={`kind${i}`} checked={p.kind === Kind.Inactive} onChange={() => update(i, { kind: Kind.Inactive })}>spielt nicht</Radio>
          </Group>
        ))}
      </div>
    </Dialog>
  );
}

const EXTRA_RULES: [keyof Rules, string][] = [
  ['sixBonus', 'bei 6 nochmal würfeln'],
  ['threeSixes', 'nach drei 6en ist Schluss'],
  ['forcedEntry', 'Raussetzen ist Pflicht'],
  ['clearStart', 'Startfeld muss geräumt werden'],
  ['firstWinnerEnds', 'Spiel endet mit dem ersten Sieger'],
];

export function RulesDialog({ controller, close }: DialogProps) {
  const g = controller.view.game;
  const [r, setR] = useState<Rules>(() => ({ ...(g && controller.view.playing ? g.pendingRules ?? g.rules : controller.settings.rules) }));
  const set = (k: keyof Rules) => (v: boolean) => setR(x => ({ ...x, [k]: v }));
  const ok = () => { controller.setRules(r); close(); };
  return (
    <Dialog title="Spielregeln" onCancel={close} class="rules" buttons={<>
      <Button kind="ok" isDefault onClick={ok} />
      <Button kind="plain" label="Original" onClick={() => setR({ ...ORIGINAL_RULES })} />
      <Button kind="cancel" onClick={close} /></>}>
      <div class="rules-columns">
        <Group title="Original">
          <Check checked={r.rest} onChange={set('rest')}>Ruhefelder</Check>
          <Check checked={r.capture} onChange={set('capture')}>Schmeisszwang</Check>
          <div class="indent">
            <Check checked={r.hard} disabled={!r.capture} onChange={set('hard')}>knallhart</Check>
            <Check checked={r.absolute} disabled={!r.capture} onChange={set('absolute')}>absolut</Check>
          </div>
          <Check checked={r.noJump} onChange={set('noJump')}>kein Selbstüberspringen</Check>
          <Check checked={r.enterOnOne} onChange={set('enterOnOne')}>auch bei 1 raussetzen</Check>
          <Check checked={r.repeatOnOne} onChange={set('repeatOnOne')}>auch bei 1 nochmal würfeln</Check>
        </Group>
        <Group title="Zusätzlich">
          {EXTRA_RULES.map(([k, label]) => <Check key={k} checked={r[k]} onChange={set(k)}>{label}</Check>)}
        </Group>
      </div>
    </Dialog>
  );
}

export function BoardDialog({ controller, close }: DialogProps) {
  const g = controller.view.game;
  const [board, setBoard] = useState<4 | 6>(g && controller.view.playing ? g.pendingBoard ?? g.playerCount : controller.settings.board);
  const ok = () => { controller.setBoard(board); close(); };
  return (
    <Dialog title="Spielbrett" onCancel={close} buttons={<><Button kind="ok" isDefault onClick={ok} /><Button kind="cancel" onClick={close} /></>}>
      <Group>
        <Radio name="board" checked={board === 4} onChange={() => setBoard(4)}>4-Spieler-Brett</Radio>
        <Radio name="board" checked={board === 6} onChange={() => setBoard(6)}>6-Spieler-Brett</Radio>
      </Group>
    </Dialog>
  );
}

const SPEEDS = ['Langsam', 'Normal', 'Schnell', 'Turbo'];

/** Klang, Tempo und Darstellung in einem Dialog. Klang wirkt sofort (zum Probehören). */
export function SettingsDialog({ controller, close }: DialogProps) {
  const initial = controller.settings;
  const [s, setS] = useState(() => ({
    speech: initial.speech, effects: initial.effects, volume: Math.round(initial.volume * 100),
    speedComputer: initial.speedComputer, speedHuman: initial.speedHuman, fastWithoutHumans: initial.fastWithoutHumans,
    showMoves: initial.showMoves, boardScale: Math.round(initial.boardScale * 100), colorblind: initial.colorblind, autoMove: initial.autoMove, allowUndo: initial.allowUndo,
  }));
  type S = typeof s;
  const settingsOf = (x: S): Partial<Settings> => ({ ...x, volume: x.volume / 100, boardScale: x.boardScale / 100 });
  const set = <K extends keyof S>(k: K, live = false) => (v: S[K]) => setS(x => {
    const next = { ...x, [k]: v };
    if (live) controller.updateSettings({ speech: next.speech, effects: next.effects, volume: next.volume / 100 });
    return next;
  });
  const ok = () => { controller.updateSettings(settingsOf(s)); close(); };
  const cancel = () => { controller.updateSettings({ speech: initial.speech, effects: initial.effects, volume: initial.volume }); close(); };
  return (
    <Dialog title="Einstellungen" onCancel={cancel} class="settings" buttons={<><Button kind="ok" isDefault onClick={ok} /><Button kind="cancel" onClick={cancel} /></>}>
      <Group title="Sound">
        <div class="two-columns">
          <Check checked={s.speech} onChange={set('speech', true)}>Sprache</Check>
          <Check checked={s.effects} onChange={set('effects', true)}>Effekte</Check>
        </div>
        <div class="slider-row">
          <Slider label="Lautstärke" value={s.volume} onInput={set('volume', true)} />
          <Button kind="plain" label="Probe" onClick={() => { controller.sound.unlock(); controller.sound.play(SOUND.info); controller.sound.play(9 + Math.floor(Math.random() * 6)); }} />
        </div>
      </Group>
      <Group title="Geschwindigkeit">
        <div class="two-columns">
          <div><div class="sub">Computer</div>{SPEEDS.map((label, i) => <Radio key={i} name="computer" checked={s.speedComputer === i} onChange={() => set('speedComputer')(i as Speed)}>{label}</Radio>)}</div>
          <div><div class="sub">Mensch</div>{SPEEDS.map((label, i) => <Radio key={i} name="human" checked={s.speedHuman === i} onChange={() => set('speedHuman')(i as Speed)}>{label}</Radio>)}</div>
        </div>
        <hr />
        <Check checked={s.fastWithoutHumans} onChange={set('fastWithoutHumans')}>Turbo, wenn alle Menschen im Ziel sind</Check>
      </Group>
      <Group title="Darstellung & Bedienung">
        <Slider label="Brettgröße" value={s.boardScale} min={35} max={100} onInput={set('boardScale')} />
        <Check checked={s.showMoves} onChange={set('showMoves')}>mögliche Züge hervorheben</Check>
        <Check checked={s.colorblind} onChange={set('colorblind')}>Farbsymbole (Farbenblind-Modus)</Check>
        <Check checked={s.autoMove} onChange={set('autoMove')}>einzigen möglichen Zug selbst ziehen</Check>
        <Check checked={s.allowUndo} onChange={set('allowUndo')}>Rückgängig erlauben</Check>
      </Group>
    </Dialog>
  );
}

export function WinnersDialog({ controller }: { controller: Controller }) {
  const g = controller.view.game;
  const winners = g?.winners ?? [];
  const finished = !g || g.finished;
  const rows = ['Sieger', '2.', '3.', '4.', '5.', '6.'].slice(0, Math.max(1, g?.playerCount ?? 4));
  return (
    <Dialog title="Gewinner" onCancel={() => controller.closeWinners(false)} class="winners" buttons={<>
      <Button kind="ok" isDefault onClick={() => controller.closeWinners(false)} />
      {!finished && !controller.view.winnersManual && <Button kind="plain" label="Ende" onClick={() => controller.closeWinners(true)} />}
    </>}>
      <Group>
        <table class="ranking">
          <tbody>
            {rows.map((label, i) => (
              <tr key={i} class={i === 0 ? 'first' : ''}>
                <th>{label}</th>
                <td>{winners[i] !== undefined && <><span class="swatch" style={{ background: PLAYERS[winners[i]].fill }} />{g!.names[winners[i]]}</>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Group>
      {!finished && !controller.view.winnersManual && <p class="hint">OK spielt um die übrigen Plätze weiter, Ende beendet das Spiel.</p>}
    </Dialog>
  );
}

export function StatsDialog({ controller, close }: DialogProps) {
  const [confirm, setConfirm] = useState(false);
  const entries = Object.entries(controller.stats).sort((a, b) => b[1].wins - a[1].wins || b[1].games - a[1].games);
  const pct = (a: number, b: number) => b > 0 ? `${Math.round((100 * a) / b)} %` : '–';
  return (
    <Dialog title="Statistik" onCancel={close} class="stats" buttons={<>
      <Button kind="ok" isDefault onClick={close} />
      {entries.length > 0 && (confirm
        ? <Button kind="no" label="Wirklich löschen" onClick={() => { controller.resetStats(); setConfirm(false); }} />
        : <Button kind="plain" label="Zurücksetzen" onClick={() => setConfirm(true)} />)}
    </>}>
      <Group>
        {entries.length === 0 ? <p>Noch keine Partien gespielt.</p> : (
          <div class="table-scroll">
            <table class="stats-table">
              <thead><tr><th>Name</th><th>Partien</th><th>Siege</th><th>Quote</th><th>Würfe</th><th>6er</th><th>geschlagen</th><th>verloren</th></tr></thead>
              <tbody>
                {entries.map(([name, s]) => (
                  <tr key={name}>
                    <th>{name}</th><td>{s.games}</td><td>{s.wins}</td><td>{pct(s.wins, s.games)}</td>
                    <td>{s.rolls}</td><td>{pct(s.sixes, s.rolls)}</td><td>{s.captures}</td><td>{s.captured}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Group>
    </Dialog>
  );
}

export function ConfirmDialog({ text, onYes, close }: { text: string; onYes: () => void; close: () => void }) {
  return (
    <Dialog title="Mensch ärgere Dich nicht!" onCancel={close} buttons={<><Button kind="yes" isDefault onClick={() => { close(); onYes(); }} /><Button kind="no" onClick={close} /></>}>
      <p class="message">{text}</p>
    </Dialog>
  );
}

export function MessageDialog({ text, close }: { text: string; close: () => void }) {
  return (
    <Dialog title="Mensch ärgere Dich nicht!" onCancel={close} buttons={<Button kind="ok" isDefault onClick={close} />}>
      <p class="message">{text}</p>
    </Dialog>
  );
}

export function InfoDialog({ close }: { close: () => void }) {
  return (
    <Dialog title="Info" onCancel={close} class="info" buttons={<Button kind="ok" isDefault onClick={close} />}>
      <Group>
        <p class="big">Mensch ärgere Dich nicht!</p>
        <p class="dim">Nach dem Windows-Spiel „Mensch ärgere Dich nicht!“ (1995) von <b>Oliver Kraheck</b> –
          Regeln, Computergegner und Klänge stammen von dort, die Grafik ist neu als Vektorgrafik gezeichnet,
          dazu kommen viele neue Funktionen.</p>
        <p class="dim">Das Original war Shareware. Registrieren lässt es sich seit vielen Jahren nicht mehr:
          Der Autor gibt keine Schlüssel mehr aus.</p>
        <p class="dim">Spielidee &amp; Gestaltung des Brettspiels: Schmidt Spiele</p>
        <p class="dim">Web-Fassung 2026: Peter W, umgesetzt mit Claude (Anthropic).</p>
      </Group>
    </Dialog>
  );
}

function slotSummary(slot: SaveSlot) {
  const g = slot.game;
  const players = g.kinds.slice(0, g.playerCount).map((k, p) => k === Kind.Inactive ? null : `${g.names[p]}${k === Kind.Computer ? ' (C)' : ''}`).filter(Boolean);
  return players.join(', ');
}
const when = (iso: string) => new Date(iso).toLocaleString('de-AT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

/** Spielstände in der App: speichern unter einem Namen, laden, löschen; Datei nur als Zusatz. */
export function SavesDialog({ controller, close, mode, onExport, onImport }: DialogProps & { mode: 'save' | 'load'; onExport: () => void; onImport: () => void }) {
  const playing = controller.view.playing;
  const [name, setName] = useState(() => `Spiel vom ${when(new Date().toISOString())}`);
  const [slots, setSlots] = useState(() => controller.slots());
  const [selected, setSelected] = useState<string | null>(slots[0]?.id ?? null);
  const save = () => { controller.saveSlot(name); close(); };
  const load = () => { if (selected) { controller.loadSlot(selected); close(); } };
  const remove = () => {
    if (!selected) return;
    controller.deleteSlot(selected);
    const next = controller.slots();
    setSlots(next);
    setSelected(next[0]?.id ?? null);
  };
  const saving = mode === 'save' && playing;
  return (
    <Dialog title={saving ? 'Spielstand speichern' : 'Spielstand laden'} onCancel={close} class="saves" buttons={saving
      ? <><Button kind="ok" isDefault onClick={save} /><Button kind="cancel" onClick={close} /></>
      : <><Button kind="ok" label="Laden" isDefault disabled={!selected} onClick={load} /><Button kind="plain" label="Löschen" disabled={!selected} onClick={remove} /><Button kind="cancel" onClick={close} /></>}>
      {saving && (
        <Group>
          <label class="bw-field"><span>Name</span><input type="text" maxLength={60} value={name} data-autofocus
            onFocus={e => (e.currentTarget as HTMLInputElement).select()}
            onInput={e => setName((e.currentTarget as HTMLInputElement).value)} /></label>
        </Group>
      )}
      <Group title={saving ? 'Bereits gespeichert' : undefined}>
        {slots.length === 0 && <p class="hint">Noch keine Spielstände gespeichert.</p>}
        <div class="slot-list" role="listbox" aria-label="Spielstände">
          {slots.map(slot => (
            <button type="button" key={slot.id} role="option" aria-selected={!saving && selected === slot.id}
              class={`slot${slot.auto ? ' auto' : ''}${!saving && selected === slot.id ? ' on' : ''}`} disabled={saving}
              onClick={() => setSelected(slot.id)} onDblClick={() => { setSelected(slot.id); controller.loadSlot(slot.id); close(); }}>
              <span class="slot-name">{slot.name}</span>
              <span class="slot-meta">{when(slot.saved)} · {slotSummary(slot)}</span>
            </button>
          ))}
        </div>
      </Group>
      <div class="file-row">
        <span class="dim">Datei (.ERG wie im Original):</span>
        {playing && <Button kind="plain" label="Exportieren" onClick={() => { onExport(); close(); }} />}
        <Button kind="plain" label="Importieren" onClick={() => { close(); onImport(); }} />
      </div>
    </Dialog>
  );
}
