import { Game, Kind } from './game';

export type Strength = 'easy' | 'normal' | 'hard';

/** Wählt den Zug des Computers für die eingestellte Stärke. */
export function chooseMove(game: Game, strength: Strength, random: () => number = Math.random): number {
  const moves = game.legalMoves();
  if (moves.length === 0) throw new Error('Kein Zug möglich.');
  if (strength === 'easy') {
    // Leicht: meist zufällig, schlägt aber gern, wenn es sich anbietet.
    const captures = moves.filter(i => game.isCapture(i));
    if (captures.length > 0 && random() < 0.5) return captures[Math.floor(random() * captures.length)];
    return moves[Math.floor(random() * moves.length)];
  }
  if (strength === 'hard') return chooseHard(game, moves);
  return chooseOriginal(game);
}

// Direkte Übersetzung von seg06:0022 (Bewertung) und 0640 (Auswahl):
// die kleinste Bewertung gewinnt, bei Gleichstand die erste Figur.
export function chooseOriginal(game: Game): number {
  const moves = game.legalMoves();
  if (moves.length === 0) throw new Error('Kein Zug möglich.');
  let best = moves[0], bestScore = originalScore(game, best);
  for (const piece of moves.slice(1)) {
    const s = originalScore(game, piece);
    if (s < bestScore) { best = piece; bestScore = s; }
  }
  return best;
}

export function originalScore(g: Game, piece: number): number {
  const from = g.position(g.player, piece), to = g.target(piece);
  if (from < 0) return 0;                                       // 0037
  if (from >= g.trackLength) return -3;                         // 0045
  if (to >= g.trackLength) return -2;                           // 005a
  const square = g.square(g.player, from), target = g.square(g.player, to);
  const stride = g.stride;
  let score = 0;
  const occupant = (field: number) => {
    for (let p = 0; p < g.playerCount; p++)
      if (g.kinds[p] !== Kind.Inactive)
        for (let i = 0; i < 4; i++) {
          const at = g.position(p, i);
          if (at >= 0 && at < g.trackLength && g.square(p, at) === field) return p;
        }
    return -1;
  };
  const finish = (p: number) => p === 0 ? g.trackLength - 1 : p * stride - 1;
  const nearFinish = (field: number, p: number) => field >= finish(p) - 7 && field <= finish(p);
  const opponent = occupant(target);
  if (opponent >= 0) score -= nearFinish(target, opponent) ? 2 : 1;   // 00e6..025d
  if (g.rules.rest && g.isRestSquare(target)) score--;               // 0260
  if (nearFinish(square, g.player)) score--;                         // 02bb
  if (g.rules.rest && g.isRestSquare(square)) score++;               // 0363
  for (let p = 0; p < g.playerCount; p++) {                           // 039d
    if (p === g.player || g.kinds[p] === Kind.Inactive) continue;
    let anyYard = false;
    for (let i = 0; i < 4; i++) if (g.position(p, i) < 0) anyYard = true;
    if (!anyYard) continue;
    if (target === p * stride) score++;
    if (square === p * stride) score--;
  }
  for (let step = 1; step < g.die; step++) {                         // 055b
    const p = occupant((square + step) % g.trackLength);
    if (p >= 0 && p !== g.player) score++;
  }
  return score;
}

// Schwer: Originalbewertung als Grundlage (sie hält u. a. das Ziel lückenlos, was die drei
// Startversuche sichert), ergänzt um Schlagbonus, Gefahrenabschätzung und das Vorziehen der
// vordersten Figur. Gegen drei Original-Gegner gewinnt sie ~28 % statt 25 % der Partien
// (test/aiTuning.bench.test.ts).
function chooseHard(g: Game, moves: number[]): number {
  const me = g.player, track = g.trackLength;
  let best = moves[0], bestValue = -Infinity;
  for (const piece of moves) {
    const from = g.position(me, piece), to = g.target(piece);
    let value = -originalScore(g, piece) * 20;
    if (g.victim(piece)) value += 10;
    if (from >= 0 && from < track) value += danger(g, me, g.square(me, from)) * (from + 10) + from;
    if (to < track) {
      const after = g.clone();
      after.move(piece);
      value -= danger(after, me, after.square(me, to)) * (to + 10);
      if (g.rules.rest && g.isRestSquare(g.square(me, to))) value += 3;
    }
    if (value > bestValue) { best = piece; bestValue = value; }
  }
  return best;
}

/** Wahrscheinlichkeit, dass ein Gegner das Feld square im nächsten Zug erreicht (grob). */
function danger(g: Game, me: number, square: number): number {
  const track = g.trackLength;
  let d = 0;
  for (let p = 0; p < g.playerCount; p++) {
    if (p === me || g.kinds[p] === Kind.Inactive || g.winners.includes(p)) continue;
    let yard = false;
    for (let j = 0; j < 4; j++) {
      const theirs = g.position(p, j);
      if (theirs < 0) { yard = true; continue; }
      if (theirs >= track) continue;
      const behind = (square - g.square(p, theirs) + track) % track;
      if (theirs + behind >= track) continue;           // biegt vorher ins eigene Ziel ab
      if (behind >= 1 && behind <= 6) d += 1 / 6;
      else if (behind >= 7 && behind <= 12) d += 1 / 36;  // Sechs und dann der Rest
    }
    if (yard && square === p * g.stride) d += g.rules.enterOnOne ? 1 / 3 : 1 / 6;
  }
  return Math.min(d, 1);
}
