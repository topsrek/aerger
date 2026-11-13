// Dialogelemente im Stil der Borland Windows Custom Controls (BWCC.DLL) des Originals:
// Stahlraster-Hintergrund, eingelassene Gruppen, Bitmap-Knöpfe mit Symbol, rautenförmige
// Optionsfelder. Die Symbole sind als Vektorgrafik nachgezeichnet, alles skaliert stufenlos.
import type { ComponentChildren } from 'preact';
import { useEffect, useRef, useState } from 'preact/hooks';

export type ButtonKind = 'ok' | 'cancel' | 'yes' | 'no' | 'help' | 'plain';

function Glyph({ kind }: { kind: ButtonKind }) {
  switch (kind) {
    case 'ok': case 'yes':
      return (
        <svg viewBox="0 0 34 30" class="glyph" aria-hidden="true">
          <path d="M5 17 l8 10 L32 3" fill="none" stroke="#000080" stroke-width="7" stroke-linejoin="round" transform="translate(2 2)" />
          <path d="M3 16 l8 10 L30 2" fill="none" stroke="#008000" stroke-width="7" stroke-linejoin="round" />
          <path d="M3 15 l8 10 L30 1" fill="none" stroke="#00ff00" stroke-width="2" stroke-linejoin="round" />
        </svg>
      );
    case 'cancel':
      return (
        <svg viewBox="0 0 30 30" class="glyph" aria-hidden="true">
          <path d="M5 5 L26 26 M26 5 L5 26" stroke="#400000" stroke-width="7" transform="translate(2 2)" />
          <path d="M4 4 L25 25 M25 4 L4 25" stroke="#c00000" stroke-width="7" />
          <path d="M4 3 L25 24 M25 3 L4 24" stroke="#ff6060" stroke-width="1.5" />
        </svg>
      );
    case 'no':
      return (
        <svg viewBox="0 0 32 32" class="glyph" aria-hidden="true">
          <g transform="translate(2 2)" stroke="#400000" fill="none" stroke-width="5"><circle cx="15" cy="15" r="11" /><path d="M7 23 L23 7" /></g>
          <g stroke="#c00000" fill="none" stroke-width="5"><circle cx="14" cy="14" r="11" /><path d="M6 22 L22 6" /></g>
        </svg>
      );
    case 'help':
      return (
        <svg viewBox="0 0 26 34" class="glyph" aria-hidden="true">
          <g fill="none" stroke-width="6" stroke-linecap="round">
            <path d="M6 10 q0 -7 8 -7 q8 0 8 7 q0 5 -7 8 v4" stroke="#000080" transform="translate(2 2)" />
            <path d="M5 9 q0 -7 8 -7 q8 0 8 7 q0 5 -7 8 v4" stroke="#00c0c0" />
          </g>
          <circle cx="16" cy="30" r="3.4" fill="#000080" /><circle cx="14" cy="28" r="3.4" fill="#00c0c0" />
        </svg>
      );
    default: return null;
  }
}

const LABEL: Record<ButtonKind, string> = { ok: 'OK', cancel: 'Abbruch', yes: 'Ja', no: 'Nein', help: 'Hilfe', plain: '' };

export function Button({ kind = 'plain', label, onClick, isDefault = false, autoFocus = false, disabled = false, wide = false }: {
  kind?: ButtonKind; label?: string; onClick: () => void; isDefault?: boolean; autoFocus?: boolean; disabled?: boolean; wide?: boolean;
}) {
  const ref = useRef<HTMLButtonElement>(null);
  useEffect(() => { if (autoFocus) ref.current?.focus(); }, [autoFocus]);
  return (
    <button ref={ref} type="button" class={`bw-button${isDefault ? ' default' : ''}${kind === 'plain' ? ' plain' : ''}${wide ? ' wide' : ''}`}
      onClick={onClick} disabled={disabled} data-default={isDefault || undefined}>
      <span class="bw-face">
        <Glyph kind={kind} />
        <span class="bw-label">{label ?? LABEL[kind]}</span>
      </span>
    </button>
  );
}

export function Check({ checked, onChange, children, disabled = false }: { checked: boolean; onChange: (v: boolean) => void; children: ComponentChildren; disabled?: boolean }) {
  return (
    <label class={`bw-choice${disabled ? ' disabled' : ''}`}>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={e => onChange((e.currentTarget as HTMLInputElement).checked)} />
      <svg class="bw-box" viewBox="0 0 13 13" aria-hidden="true">
        <path d="M0.5 12.5 V0.5 H12.5" stroke="#fff" fill="none" />
        <path d="M0.5 12.5 H12.5 V0.5" stroke="#808080" fill="none" />
        {checked && <path d="M2 7 l2 3 l1 0 l6 -7" stroke="#000" stroke-width="2" fill="none" />}
      </svg>
      <span class="bw-text">{children}</span>
    </label>
  );
}

export function Radio({ name, checked, onChange, children, disabled = false }: { name: string; checked: boolean; onChange: () => void; children: ComponentChildren; disabled?: boolean }) {
  return (
    <label class={`bw-choice${disabled ? ' disabled' : ''}`}>
      <input type="radio" name={name} checked={checked} disabled={disabled} onChange={onChange} />
      <svg class="bw-box" viewBox="0 0 13 13" aria-hidden="true">
        <path d="M6.5 0.5 L0.5 6.5 L6.5 12.5" stroke="#808080" fill="none" />
        <path d="M6.5 0.5 L12.5 6.5 L6.5 12.5" stroke="#fff" fill="none" />
        {checked && <path d="M6.5 4 L9 6.5 L6.5 9 L4 6.5Z" fill="#000" />}
      </svg>
      <span class="bw-text">{children}</span>
    </label>
  );
}

export function Group({ title, children, class: cls = '' }: { title?: ComponentChildren; children: ComponentChildren; class?: string }) {
  return (
    <div class={`bw-group ${cls}`}>
      {title && <div class="bw-group-title">{title}</div>}
      <div class="bw-group-body">{children}</div>
    </div>
  );
}

export function Slider({ value, onInput, label, min = 0, max = 100 }: { value: number; onInput: (v: number) => void; label: string; min?: number; max?: number }) {
  return (
    <label class="bw-slider">
      <span class="bw-text">{label}</span>
      <input type="range" min={min} max={max} value={value} onInput={e => onInput(Number((e.currentTarget as HTMLInputElement).value))} aria-label={label} />
      <span class="bw-value">{value}</span>
    </label>
  );
}

/** Modaler Dialog: Escape bricht ab, Enter löst den Standardknopf aus, Fokus bleibt im Dialog. */
export function Dialog({ title, onCancel, children, buttons, class: cls = '' }: {
  title: string; onCancel: () => void; children: ComponentChildren; buttons: ComponentChildren; class?: string;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [opened] = useState(() => document.activeElement as HTMLElement | null);
  useEffect(() => {
    const el = ref.current!;
    if (!el.contains(document.activeElement)) {
      // Erst ausdrücklich gewünschtes Ziel, dann Eingabefeld, dann Standardknopf; nie das ×.
      const q = (sel: string) => el.querySelector<HTMLElement>(sel);
      (q('[data-autofocus]') ?? q('.bw-body input') ?? q('button[data-default]') ?? q('.bw-buttons button') ?? el).focus();
    }
    return () => { if (opened && !opened.closest('.toolbar')) opened.focus?.(); };
  }, []);
  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); onCancel(); return; }
    if (e.key === 'Enter' && !(e.target instanceof HTMLButtonElement) && !(e.target instanceof HTMLTextAreaElement)) {
      const def = ref.current!.querySelector<HTMLButtonElement>('button[data-default]');
      if (def) { e.preventDefault(); def.click(); }
      return;
    }
    if (e.key === 'Tab') {
      const items = [...ref.current!.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex="0"]')]
        .filter(x => !(x instanceof HTMLInputElement && x.type === 'radio' && !x.checked));
      if (items.length === 0) return;
      const i = items.indexOf(document.activeElement as HTMLElement);
      if (e.shiftKey && i <= 0) { e.preventDefault(); items[items.length - 1].focus(); }
      else if (!e.shiftKey && i === items.length - 1) { e.preventDefault(); items[0].focus(); }
    }
    e.stopPropagation();
  };
  return (
    <div class="bw-overlay" onPointerDown={e => { if (e.target === e.currentTarget) onCancel(); }}>
      <div ref={ref} class={`bw-dialog ${cls}`} role="dialog" aria-modal="true" aria-label={title} tabIndex={-1} onKeyDown={onKey}>
        <div class="bw-title">
          <span>{title}</span>
          <button type="button" class="bw-close" aria-label="Schließen" onClick={onCancel}>×</button>
        </div>
        <div class="bw-body">{children}</div>
        <div class="bw-buttons">{buttons}</div>
      </div>
    </div>
  );
}
