// Werkzeugleiste: am Rand neben dem Brett (Querformat) bzw. unter dem Brett (Hochformat).
// In niedrigen Fenstern ("rail": nur Symbole) und unten stehen die selteneren Punkte unter „Mehr“.
import { Fragment } from 'preact';
import { useEffect, useRef, useState } from 'preact/hooks';

export type IconName = 'new' | 'undo' | 'players' | 'board' | 'rules' | 'settings' | 'stats' | 'winners' | 'collapse' | 'expand' | 'continue' | 'save' | 'load' | 'help' | 'info' | 'more';

const ICONS: Record<IconName, preact.JSX.Element> = {
  new: <><path d="M12 5v14M5 12h14" /></>,
  undo: <><path d="M9 14 4 9l5-5" /><path d="M4 9h10a6 6 0 0 1 0 12h-3" /></>,
  players: <><circle cx="9" cy="8" r="3.5" /><path d="M2.5 20a6.5 6.5 0 0 1 13 0" /><circle cx="17" cy="9" r="2.5" /><path d="M16.5 14.5a5 5 0 0 1 5 5.5" /></>,
  board: <><rect x="3.5" y="3.5" width="17" height="17" rx="2.5" /><circle cx="8.5" cy="8.5" r="1.5" /><circle cx="15.5" cy="15.5" r="1.5" /><circle cx="15.5" cy="8.5" r="1.5" /><circle cx="8.5" cy="15.5" r="1.5" /></>,
  rules: <><path d="M5 4h11l3 3v13H5z" /><path d="M9 10h6M9 14h6M9 18h3" /></>,
  settings: <><circle cx="12" cy="12" r="3" /><path d="M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1" /></>,
  stats: <><path d="M4 20V10M10 20V4M16 20v-7M22 20H2" /></>,
  winners: <><path d="M7 4h10v5a5 5 0 0 1-10 0z" /><path d="M7 6H4v1a3 3 0 0 0 3 3M17 6h3v1a3 3 0 0 1-3 3M12 14v4M8 20h8" /></>,
  save: <><path d="M12 3v12M7 10l5 5 5-5" /><path d="M4 17v3h16v-3" /></>,
  load: <><path d="M12 15V3M7 8l5-5 5 5" /><path d="M4 17v3h16v-3" /></>,
  help: <><circle cx="12" cy="12" r="9" /><path d="M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.7.3-1 .9-1 1.7" /><circle cx="12" cy="17" r=".6" /></>,
  info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v6" /><circle cx="12" cy="7.5" r=".6" /></>,
  collapse: <><path d="M14 6l-6 6 6 6" /><path d="M20 6l-6 6 6 6" /></>,
  expand: <><path d="M10 6l6 6-6 6" /><path d="M4 6l6 6-6 6" /></>,
  continue: <><path d="M7 4.5v15l12-7.5z" /></>,
  more: <><circle cx="5" cy="12" r="1.2" /><circle cx="12" cy="12" r="1.2" /><circle cx="19" cy="12" r="1.2" /></>,
};

function Icon({ name }: { name: IconName }) {
  return <svg class="tb-icon" viewBox="0 0 24 24" aria-hidden="true">{ICONS[name]}</svg>;
}

export interface Tool {
  icon: IconName; label: string; action: () => void;
  /** Kürzere Beschriftung für die Leiste unten. */
  short?: string;
  shortcut?: string; disabled?: boolean; primary?: boolean;
  /** Im Hochformat direkt sichtbar (sonst unter „Mehr“). */
  main?: boolean;
  /** Abstand davor (Gruppierung in der Seitenleiste). */
  gap?: boolean;
}

function ToolButton({ tool, onDone, compact = false }: { tool: Tool; onDone?: () => void; compact?: boolean }) {
  return (
    <button type="button" class={`tb-button${tool.primary ? ' primary' : ''}`} disabled={tool.disabled}
      title={tool.shortcut ? `${tool.label} (${tool.shortcut})` : tool.label}
      onClick={(e) => { onDone?.(); if (e.detail > 0) (e.currentTarget as HTMLElement).blur(); tool.action(); }}>
      <span class="tb-face"><Icon name={tool.icon} /><span class="tb-label">{compact ? tool.short ?? tool.label : tool.label}</span></span>
    </button>
  );
}

export type ToolbarMode = 'side' | 'rail' | 'bottom';

export function Toolbar({ tools, mode, collapsed, onCollapse, onOpenChange }: {
  tools: Tool[]; mode: ToolbarMode; collapsed: boolean; onCollapse: (collapsed: boolean) => void; onOpenChange: (open: boolean) => void;
}) {
  const [more, setMore] = useState(false);
  const ref = useRef<HTMLElement>(null);
  useEffect(() => { onOpenChange(more); }, [more]);
  useEffect(() => { setMore(false); }, [mode]);
  useEffect(() => {
    if (!more) return;
    const away = (e: PointerEvent) => { if (!ref.current?.contains(e.target as Node)) setMore(false); };
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); setMore(false); } };
    document.addEventListener('pointerdown', away);
    document.addEventListener('keydown', key);
    ref.current?.querySelector<HTMLElement>('.tb-more button:not([disabled])')?.focus();
    return () => { document.removeEventListener('pointerdown', away); document.removeEventListener('keydown', key); };
  }, [more]);

  if (mode === 'side') {
    return (
      <nav class={`toolbar side${collapsed ? ' collapsed' : ''}`} ref={ref} aria-label="Spiel">
        {tools.map(t => <Fragment key={t.icon}>{t.gap && <span class="tb-gap" />}<ToolButton tool={t} /></Fragment>)}
        <span class="tb-spacer" />
        <ToolButton tool={collapsed
          ? { icon: 'expand', label: 'Leiste aufklappen', action: () => onCollapse(false) }
          : { icon: 'collapse', label: 'Einklappen', action: () => onCollapse(true) }} />
      </nav>
    );
  }
  const rest = tools.filter(t => !t.main);
  return (
    <nav class={`toolbar ${mode}`} ref={ref} aria-label="Spiel">
      {tools.filter(t => t.main).map(t => <ToolButton key={t.icon} tool={t} compact={mode === 'bottom'} />)}
      <div class="tb-more-wrap">
        <button type="button" class={`tb-button${more ? ' on' : ''}`} aria-expanded={more} title="Mehr" onClick={() => setMore(!more)}>
          <span class="tb-face"><Icon name="more" /><span class="tb-label">Mehr</span></span>
        </button>
        {more && (
          <div class="tb-more" role="menu">
            {rest.map(t => <ToolButton key={t.icon} tool={t} onDone={() => setMore(false)} />)}
          </div>
        )}
      </div>
    </nav>
  );
}
