// Spielstandformat des Originals (.ERG, 331 Bytes): seg12:0002 (schreiben) und 0407 (lesen).
import { Game, Kind, ORIGINAL_RULE_KEYS, rules } from './game';

const decoder = new TextDecoder('windows-1252');
const HIGH = '€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ';
function encode1252(text: string): number[] {
  const out: number[] = [];
  for (const ch of text) {
    const code = ch.codePointAt(0)!;
    const high = HIGH.indexOf(ch);
    out.push(high >= 0 ? 0x80 + high : code < 256 ? code : 0x3f);
  }
  return out;
}

export class InvalidSaveError extends Error {
  constructor() { super('Ungültiger ERG-Spielstand.'); }
}

export function readErg(bytes: Uint8Array): Game {
  const require = (valid: boolean) => { if (!valid) throw new InvalidSaveError(); };
  require(bytes.length === 331);
  require((bytes[138] === 4 || bytes[138] === 6) && bytes[139] === (bytes[138] === 6 ? 1 : 0));
  for (let i = 140; i < 147; i++) require(bytes[i] <= 1);
  require(bytes[327] <= 1 && bytes[328] <= 6 && bytes[329] <= 3 && bytes[330] >= 1 && bytes[330] <= bytes[138]);
  require(bytes[327] === 0 || bytes[328] > 0);
  const players = bytes[138] as 4 | 6;
  const g = new Game(players, bytes[330] - 1);
  g.rules = rules(Object.fromEntries(ORIGINAL_RULE_KEYS.map((k, i) => [k, bytes[140 + i] !== 0])));
  const occupied = new Set<number>();
  for (let p = 0; p < 6; p++) {
    const o = p * 23;
    require(bytes[o] <= 21 && bytes[o + 22] <= 2);
    g.names[p] = decoder.decode(bytes.subarray(o + 1, o + 1 + bytes[o]));
    g.kinds[p] = bytes[o + 22] as Kind;
    const own = new Set<number>();
    for (let i = 0; i < 4; i++) {
      const field = bytes[147 + p * 4 + i];
      // Figuren inaktiver Spieler und der Spieler 5/6 auf dem Viererbrett werden nie gezeichnet
      // oder gelesen (seg17:0405); das Original lässt dort alte Werte stehen.
      if (p >= g.playerCount || g.kinds[p] === Kind.Inactive) {
        require(field === 0 || (field >= 1 && field <= 48) || field === 101 + i || (field >= 201 && field <= 204));
        g.setPosition(p, i, -1);
        continue;
      }
      require(field === 101 + i || (field >= 201 && field <= 204) || (field >= 1 && field <= g.trackLength));
      const position = field >= 101 && field <= 104 ? -1 : field >= 201 ? g.trackLength + field - 201
        : (field - 1 - p * g.stride + g.trackLength) % g.trackLength;
      g.setPosition(p, i, position);
      if (position < 0) continue;
      require(!own.has(position)); own.add(position);
      if (position < g.trackLength) { require(!occupied.has(field)); occupied.add(field); }
    }
  }
  for (const winner of bytes.subarray(321, 327)) {
    if (winner === 0) continue;
    require(winner <= 6 && !g.winners.includes(winner - 1));
    g.winners.push(winner - 1);
  }
  g.restoreTurn(bytes[330] - 1, bytes[328], bytes[327] !== 0, bytes[329]);
  return g;
}

export function writeErg(g: Game): Uint8Array {
  const b = new Uint8Array(331);
  for (let p = 0; p < 6; p++) {
    const name = encode1252(g.names[p]).slice(0, 21), o = p * 23;
    b[o] = name.length; b.set(name, o + 1);
    b[o + 22] = g.kinds[p];
    for (let i = 0; i < 4; i++) {
      const at = g.position(p, i), n = p * 4 + i;
      // seg04:01ca setzt Figuren deaktivierter Spieler samt Haus-/Zielflags auf null.
      if (g.kinds[p] === Kind.Inactive) continue;
      b[147 + n] = at < 0 ? 101 + i : at >= g.trackLength ? 201 + at - g.trackLength : g.square(p, at) + 1;
      b[267 + n] = at < 0 ? 1 : 0;
      if (at >= g.trackLength) b[291 + p * 4 + at - g.trackLength] = i + 1;
      if (at >= 0 && at < g.trackLength && p < g.playerCount) {
        const field = g.square(p, at); b[171 + field * 2] = p + 1; b[172 + field * 2] = i + 1;
      }
    }
  }
  b[138] = g.playerCount; b[139] = g.playerCount === 6 ? 1 : 0;
  ORIGINAL_RULE_KEYS.forEach((k, i) => { b[140 + i] = g.rules[k] ? 1 : 0; });
  g.winners.forEach((w, i) => { b[315 + w] = 1; b[321 + i] = w + 1; });
  b[327] = g.awaitingMove ? 1 : 0; b[328] = g.die; b[329] = g.attempts; b[330] = g.player + 1;
  return b;
}
