// Einstellungen, Autosave und Statistik im localStorage.
import { Kind, rules, type GameData, type Rules } from '../core/game';
import type { Strength } from '../core/strategy';

export type Speed = 0 | 1 | 2 | 3;

export interface PlayerSetup { name: string; kind: Kind; strength: Strength }

export interface Settings {
  players: PlayerSetup[];
  board: 4 | 6;
  rules: Rules;
  speedHuman: Speed;
  speedComputer: Speed;
  /** Computer auf Turbo, sobald alle Menschen im Ziel sind. */
  fastWithoutHumans: boolean;
  speech: boolean;
  effects: boolean;
  volume: number;
  showMoves: boolean;
  /** Brettgröße als Anteil der freien Fläche (0.35 … 1). */
  boardScale: number;
  /** Seitenleiste nur mit Symbolen. */
  sidebarCollapsed: boolean;
  colorblind: boolean;
  autoMove: boolean;
  allowUndo: boolean;
}

export const DEFAULT_SETTINGS: Settings = {
  players: Array.from({ length: 6 }, (_, p) => ({ name: `Spieler ${p + 1}`, kind: p === 0 ? Kind.Human : Kind.Computer, strength: 'normal' as Strength })),
  board: 4,
  rules: rules(),
  speedHuman: 1,
  speedComputer: 1,
  fastWithoutHumans: true,
  speech: true,
  effects: true,
  volume: 0.8,
  showMoves: true,
  boardScale: 1,
  sidebarCollapsed: false,
  colorblind: false,
  autoMove: false,
  allowUndo: true,
};

export interface SavedGame { game: GameData; undo: GameData[]; shownDie: number }

export interface PlayerStats {
  games: number; wins: number; places: number[]; rolls: number; sixes: number; captures: number; captured: number;
}
export type Stats = Record<string, PlayerStats>;

const KEYS = { settings: 'aerger.settings.v1', save: 'aerger.save.v1', stats: 'aerger.stats.v1', slots: 'aerger.slots.v1' };

function read<T>(key: string): T | null {
  try { const text = localStorage.getItem(key); return text ? JSON.parse(text) as T : null; } catch { return null; }
}
function write(key: string, value: unknown) {
  try { if (value === null) localStorage.removeItem(key); else localStorage.setItem(key, JSON.stringify(value)); } catch { /* Speicher voll oder gesperrt */ }
}

export function loadSettings(): Settings {
  const stored = read<Partial<Settings>>(KEYS.settings);
  if (!stored) return structuredClone(DEFAULT_SETTINGS);
  const players = DEFAULT_SETTINGS.players.map((d, p) => ({ ...d, ...stored.players?.[p] }));
  return { ...structuredClone(DEFAULT_SETTINGS), ...stored, players, rules: rules(stored.rules) };
}
export const saveSettings = (s: Settings) => write(KEYS.settings, s);

export const loadGame = () => read<SavedGame>(KEYS.save);
export const saveGame = (g: SavedGame | null) => write(KEYS.save, g);

/** Gespeicherter Spielstand in der App (Browser-Speicher), neuester zuerst. */
export interface SaveSlot { id: string; name: string; saved: string; game: GameData; /** automatisch beim Ersetzen der Partie gesichert */ auto?: boolean }
export const loadSlots = (): SaveSlot[] => read<SaveSlot[]>(KEYS.slots) ?? [];
export const saveSlots = (slots: SaveSlot[]) => write(KEYS.slots, slots);

export const loadStats = (): Stats => read<Stats>(KEYS.stats) ?? {};
export const saveStats = (s: Stats) => write(KEYS.stats, s);
export const emptyStats = (): PlayerStats => ({ games: 0, wins: 0, places: [0, 0, 0, 0, 0, 0], rolls: 0, sixes: 0, captures: 0, captured: 0 });
