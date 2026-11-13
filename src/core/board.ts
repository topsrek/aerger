// Brettgeometrie im 700er-Koordinatensystem des Originals (Feldursprünge aus AERGER.EXE).
import { FIELD_SIZE, GOAL_FOUR, GOAL_SIX, REST_CORNERS_FOUR, REST_CORNERS_SIX, TRACK_FOUR, TRACK_SIX, YARD_FOUR, YARD_SIX, type Pt } from './boardTables';
import type { Game } from './game';

export const BOARD_SIZE = 700;

export interface BoardGeometry {
  six: boolean;
  fieldSize: number;
  track: readonly Pt[];
  yard: readonly Pt[][];
  goal: readonly Pt[][];
  restCorners: readonly Pt[];
}

export function geometry(players: 4 | 6): BoardGeometry {
  const six = players === 6;
  return six
    ? { six, fieldSize: FIELD_SIZE.six, track: TRACK_SIX, yard: YARD_SIX, goal: GOAL_SIX, restCorners: REST_CORNERS_SIX }
    : { six, fieldSize: FIELD_SIZE.four, track: TRACK_FOUR, yard: YARD_FOUR, goal: GOAL_FOUR, restCorners: REST_CORNERS_FOUR };
}

/** Feldursprung (links oben) einer Figur an einer Position: Haus = Figurnummer, Bahn, Ziel. */
export function origin(game: Game, player: number, piece: number, position: number): Pt {
  const geo = geometry(game.playerCount);
  if (position < 0) return geo.yard[player][piece];
  if (position < game.trackLength) return geo.track[game.square(player, position)];
  return geo.goal[player][position - game.trackLength];
}

/** Mittelpunkt des Feldes. */
export function center(game: Game, player: number, piece: number, position: number): Pt {
  const [x, y] = origin(game, player, piece, position);
  const half = geometry(game.playerCount).fieldSize / 2;
  return [x + half, y + half];
}
