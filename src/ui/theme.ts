// Farben und Namen aus den Originalgrafiken (GRAPH02/03) und dem Spielerdialog.

export const BOARD_YELLOW = '#ffff4b';
export const FRAME_RED = '#ff0000';

export interface PlayerStyle {
  name: string;
  fill: string;
  /** Kontrastfarbe für Symbole und Gesichtszüge. */
  ink: string;
  /** Symbol im Farbenblind-Modus. */
  symbol: 'circle' | 'triangle' | 'square' | 'diamond' | 'star' | 'cross';
}

export const PLAYERS: readonly PlayerStyle[] = [
  { name: 'Gelb', fill: '#ffd343', ink: '#000', symbol: 'circle' },
  { name: 'Grün', fill: '#00ff00', ink: '#000', symbol: 'triangle' },
  { name: 'Rot', fill: '#ff0000', ink: '#000', symbol: 'square' },
  { name: 'Blau', fill: '#5353ff', ink: '#000', symbol: 'diamond' },
  { name: 'Lila', fill: '#ff8fff', ink: '#000', symbol: 'star' },
  { name: 'Schwarz', fill: '#000000', ink: '#fff', symbol: 'cross' },
];
