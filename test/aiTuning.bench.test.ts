// Stärkevergleich der Computergegner (nicht Teil von npm test): npm run bench
import { it } from 'vitest';
import { Game, Kind, rules } from '../src/core/game';
import { chooseMove, type Strength } from '../src/core/strategy';

function winRate(seats: Strength[], seed: number, games: number) {
  let a = seed >>> 0;
  const rnd = () => { a = (a + 0x6d2b79f5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
  let wins = 0;
  for (let k = 0; k < games; k++) {
    const g = new Game(4, k % 4); g.rules = rules({ firstWinnerEnds: true }); g.kinds.fill(Kind.Computer);
    const rot = k % 4;   // Sitzplätze rotieren
    for (let t = 0; t < 50000 && !g.finished; t++) {
      g.roll(1 + Math.floor(rnd() * 6));
      if (!g.legalMoves().length) g.pass(); else g.move(chooseMove(g, seats[(g.player + rot) % 4], rnd));
    }
    if ((g.winner + rot) % 4 === 0) wins++;
  }
  return (wins / games).toFixed(3);
}

it('Siegquote von Sitz 1 (Zufall: 0,25)', () => {
  console.log('normal vs 3 normal', winRate(['normal', 'normal', 'normal', 'normal'], 11, 4000));
  console.log('hard vs 3 normal', winRate(['hard', 'normal', 'normal', 'normal'], 11, 4000));
  console.log('normal vs 3 easy', winRate(['normal', 'easy', 'easy', 'easy'], 11, 4000));
  console.log('hard vs 3 easy', winRate(['hard', 'easy', 'easy', 'easy'], 11, 4000));
}, 900_000);
