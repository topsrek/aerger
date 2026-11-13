import { useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'preact/compat';
import { usesOnlyOriginalRules } from '../core/game';
import { Controller } from './controller';
import {
  BoardDialog, ConfirmDialog, InfoDialog, MessageDialog, PlayersDialog, RulesDialog, SavesDialog, SettingsDialog, StatsDialog, WinnersDialog,
} from './dialogs';
import { HelpDialog, type HelpTopic } from './help';
import { SOUND } from './sound';
import { LANDSCAPE, PORTRAIT, Stage } from './Stage';
import { Toolbar, type Tool, type ToolbarMode } from './Toolbar';

type DialogName = 'players' | 'rules' | 'board' | 'settings' | 'stats' | 'save' | 'load' | 'info' | 'confirmNew' | 'confirmEnd' | 'loadError' | `help:${HelpTopic}`;

const controller = new Controller();
// Für Ablauftests (test/e2e) und Fehlersuche in der Konsole.
(window as unknown as { aerger: Controller }).aerger = controller;

function download(name: string, data: BlobPart, type: string) {
  const url = URL.createObjectURL(new Blob([data], { type }));
  const a = Object.assign(document.createElement('a'), { href: url, download: name });
  document.body.append(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Werkzeugleiste unten bei Hochkant-Fenstern, sonst am Rand (in niedrigen Fenstern nur Symbole). */
function toolbarMode(): ToolbarMode {
  if (window.innerHeight > window.innerWidth) return 'bottom';
  return window.innerHeight < 600 ? 'rail' : 'side';
}

const MIN_SCALE = 0.35;
const clampScale = (s: number) => Math.round(Math.min(1, Math.max(MIN_SCALE, s)) * 100) / 100;

/** Griff an der Brettecke: Ziehen ändert die Größe, Doppelklick passt wieder ins Fenster ein. */
function ResizeGrip({ scale, onDrag, onDone }: { scale: number; onDrag: (s: number) => void; onDone: (s: number) => void }) {
  const start = useRef<{ x: number; y: number; w: number; h: number; scale: number; last: number } | null>(null);
  const onDown = (e: PointerEvent) => {
    e.preventDefault(); e.stopPropagation();
    const box = (e.currentTarget as HTMLElement).parentElement!.getBoundingClientRect();
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    start.current = { x: e.clientX, y: e.clientY, w: box.width, h: box.height, scale, last: scale };
  };
  const onMove = (e: PointerEvent) => {
    const s = start.current;
    if (!s) return;
    // Das Brett steht mittig: die Ecke bewegt sich nur halb so weit wie die Breite wächst.
    const grow = Math.max((s.w + 2 * (e.clientX - s.x)) / s.w, (s.h + 2 * (e.clientY - s.y)) / s.h);
    s.last = clampScale(s.scale * grow);
    onDrag(s.last);
  };
  const onUp = () => { const s = start.current; start.current = null; if (s) onDone(s.last); };
  const onKey = (e: KeyboardEvent) => {
    const step = e.key === 'ArrowUp' || e.key === 'ArrowRight' || e.key === '+' ? 0.05 : e.key === 'ArrowDown' || e.key === 'ArrowLeft' || e.key === '-' ? -0.05 : 0;
    if (step) { e.preventDefault(); e.stopPropagation(); onDone(clampScale(scale + step)); }
  };
  return (
    <div class="resize-grip" role="slider" tabIndex={0} aria-label="Brettgröße" aria-valuemin={MIN_SCALE * 100} aria-valuemax={100} aria-valuenow={Math.round(scale * 100)}
      title="Brettgröße ziehen (Doppelklick: ganzes Fenster)"
      onPointerDown={onDown} onPointerMove={onMove} onPointerUp={onUp} onPointerCancel={onUp}
      onDblClick={() => onDone(1)} onKeyDown={onKey} onClick={e => e.stopPropagation()}>
      <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M15 6 L6 15 M15 10 L10 15 M15 14 L14 15" /></svg>
    </div>
  );
}

export function App() {
  const view = useSyncExternalStore(cb => controller.subscribe(cb), () => controller.view);
  const [dialog, setDialog] = useState<DialogName | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [area, setArea] = useState({ width: 0, height: 0 });
  const [mode, setMode] = useState<ToolbarMode>(toolbarMode);
  /** Brettgröße während des Ziehens am Griff (sonst aus den Einstellungen). */
  const [dragScale, setDragScale] = useState<number | null>(null);
  const scale = dragScale ?? controller.settings.boardScale;
  const stageArea = { width: area.width * scale, height: area.height * scale };
  const main = useRef<HTMLElement>(null);
  const file = useRef<HTMLInputElement>(null);
  const playing = view.playing;
  const notice = view.notice;
  const modal = dialog !== null || view.winnersOpen || notice !== null || menuOpen;

  useEffect(() => { controller.setModal(modal); }, [modal]);
  useEffect(() => { controller.sound.welcome(); controller.schedule(); }, []);

  useEffect(() => {
    const onResize = () => setMode(toolbarMode());
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);
  // Freie Fläche fürs Brett messen; die Spielfläche wird darin eingepasst (nie Scrollbalken).
  useEffect(() => {
    const el = main.current!;
    const measure = () => setArea({ width: Math.max(0, el.clientWidth - 12), height: Math.max(0, el.clientHeight - 12) });
    const observer = new ResizeObserver(measure);
    observer.observe(el);
    measure();
    return () => observer.disconnect();
  }, []);
  // Hochformat, sobald das Brett mit Spielerfeld darunter größer dargestellt werden kann.
  const land = Math.min(stageArea.width / LANDSCAPE.width, stageArea.height / LANDSCAPE.height);
  const port = Math.min(stageArea.width / PORTRAIT.width, stageArea.height / PORTRAIT.height);
  const portrait = port > land * 1.04;

  const open = (name: DialogName) => { controller.sound.unlock(); setDialog(name); };
  const close = () => setDialog(null);
  const newGame = () => { if (playing) open('confirmNew'); else controller.newGame(); };
  const exportFile = () => {
    const erg = controller.exportErg();
    if (!erg) return;
    const stamp = new Date().toISOString().slice(0, 16).replace(/[-:T]/g, '');
    // Mit Zusatzregeln passt der Stand nicht ins Original-Format: dann JSON.
    if (usesOnlyOriginalRules(controller.game!.rules)) download(`aerger-${stamp}.erg`, erg as Uint8Array<ArrayBuffer>, 'application/octet-stream');
    else download(`aerger-${stamp}.json`, controller.exportJson()!, 'application/json');
  };
  const load = async (input: HTMLInputElement) => {
    const f = input.files?.[0];
    input.value = '';
    if (!f) return;
    try { controller.importFile(new Uint8Array(await f.arrayBuffer())); } catch { open('loadError'); }
  };

  // Tastatur: Original (Leertaste/Enter/Tab) plus neue Kürzel.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      controller.sound.unlock();
      if (e.key === 'F1') { e.preventDefault(); open('help:rules'); return; }
      if (dialog || view.winnersOpen || notice || menuOpen) return;
      if (e.key === 'F2') { e.preventDefault(); newGame(); return; }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') { e.preventDefault(); controller.undo(); return; }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); if (playing) open('save'); return; }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'o') { e.preventDefault(); open('load'); return; }
      if (e.target instanceof HTMLElement && e.target.closest('button, input, select, a')) return;
      if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); controller.keyRequest('enter'); }
      else if (e.key === 'Tab') { e.preventDefault(); controller.keyRequest(e.shiftKey ? 'shift-tab' : 'tab'); }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [dialog, view.winnersOpen, notice, menuOpen, playing]);

  /** Auf dem Titelbild: die zuletzt automatisch gesicherte Partie fortsetzen. */
  const continueTool = (): Tool[] => {
    const last = controller.slots().find(s => s.auto);
    return last ? [{ icon: 'continue', label: 'Weiterspielen', short: 'Weiter', action: () => controller.loadSlot(last.id), main: true }] : [];
  };
  const tools: Tool[] = useMemo(() => [
    { icon: 'new', label: 'Neues Spiel', short: 'Neu', shortcut: 'F2', action: newGame, primary: true, main: true },
    ...(playing ? [] : continueTool()),
    { icon: 'undo', label: 'Zurück', shortcut: 'Strg+Z', action: () => controller.undo(), disabled: !controller.canUndo, main: true },
    { icon: 'players', label: 'Spieler', action: () => open('players'), main: true, gap: true },
    { icon: 'board', label: 'Brett', action: () => open('board') },
    { icon: 'rules', label: 'Regeln', action: () => open('rules'), main: true },
    { icon: 'settings', label: 'Einstellungen', short: 'Optionen', action: () => open('settings'), main: true },
    { icon: 'save', label: 'Speichern', shortcut: 'Strg+S', action: () => open('save'), disabled: !playing, gap: true },
    { icon: 'load', label: 'Laden', shortcut: 'Strg+O', action: () => open('load') },
    { icon: 'winners', label: 'Rangliste', action: () => controller.openWinners(true), disabled: !playing },
    { icon: 'stats', label: 'Statistik', action: () => open('stats') },
    { icon: 'help', label: 'Hilfe', shortcut: 'F1', action: () => open('help:rules'), gap: true },
    { icon: 'info', label: 'Info', action: () => { controller.sound.unlock(); controller.sound.play(SOUND.info); open('info'); } },
  ], [playing, view]);

  const props = { controller, close };
  return (
    <div class={`app ${mode}`}>
      <main ref={main} class="stage-wrap">
        {area.width > 0 && (
          <div class="stage-box">
            <Stage controller={controller} view={view} portrait={portrait} area={stageArea} />
            <ResizeGrip scale={scale} onDrag={setDragScale} onDone={s => { setDragScale(null); controller.updateSettings({ boardScale: s }); }} />
          </div>
        )}
      </main>
      <Toolbar tools={tools} mode={mode} collapsed={controller.settings.sidebarCollapsed}
        onCollapse={c => controller.updateSettings({ sidebarCollapsed: c })} onOpenChange={setMenuOpen} />
      <input ref={file} type="file" accept=".erg,.json,application/json" hidden onChange={e => load(e.currentTarget as HTMLInputElement)} />
      {dialog === 'players' && <PlayersDialog {...props} />}
      {dialog === 'rules' && <RulesDialog {...props} />}
      {dialog === 'board' && <BoardDialog {...props} />}
      {dialog === 'settings' && <SettingsDialog {...props} />}
      {dialog === 'stats' && <StatsDialog {...props} />}
      {(dialog === 'save' || dialog === 'load') && <SavesDialog {...props} mode={dialog} onExport={exportFile} onImport={() => file.current?.click()} />}
      {dialog === 'info' && <InfoDialog close={close} />}
      {dialog?.startsWith('help:') && <HelpDialog topic={dialog.slice(5) as HelpTopic} close={close} />}
      {dialog === 'confirmNew' && <ConfirmDialog text="Wollen Sie das Spiel abbrechen?" close={close} onYes={() => controller.newGame()} />}
      {dialog === 'confirmEnd' && <ConfirmDialog text="Wollen Sie das Spiel abbrechen?" close={close} onYes={() => controller.endGame()} />}
      {dialog === 'loadError' && <MessageDialog text="Die Datei ist kein gültiger Spielstand." close={close} />}
      {!dialog && view.winnersOpen && <WinnersDialog controller={controller} />}
      {!dialog && notice === 'changes' && <MessageDialog text="Die Änderungen werden nach dem nächsten Zug wirksam." close={() => controller.closeNotice()} />}
      {!dialog && notice === 'boardRejected' && <MessageDialog text="Ein Umschalten auf das 4-Spieler-Brett ist zur Zeit nicht möglich." close={() => controller.closeNotice()} />}
      {!dialog && notice === 'noPlayers' && <MessageDialog text="Es spielt niemand mit. Bitte unter „Spieler“ mindestens einen Spieler einstellen." close={() => { controller.closeNotice(); open('players'); }} />}
    </div>
  );
}
