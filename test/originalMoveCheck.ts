// Direkte Übersetzung der Original-Zugprüfung seg13:1140 (Zustandsmaschine 043d, Helfer
// 0012/00da/0244, Schlagprüfung 0e32). Nur Testorakel für Game.legalMoves; arbeitet in der
// Original-Kodierung: Bahn 1..40/48, Haus 101..104, Ziel 201..204.
import { Game, Kind, type Rules } from '../src/core/game';

const Illegal = 0xfd, Legal = 0xfe;

interface Position {
  game: Game; me: number; die: number; trackLength: number; start: number; finish: number;
  occupant: number[]; goal: boolean[]; yard: boolean[]; pieces: number[]; finishQuirk: boolean;
  canEnter: boolean; anyYard: boolean; anyCapture: boolean; rules: Rules;
}

export function encode(game: Game, player: number, piece: number) {
  const at = game.position(player, piece);
  if (at < 0) return 101 + piece;
  if (at < game.trackLength) return game.square(player, at) + 1;
  return 201 + at - game.trackLength;
}

export function originalLegalMoves(game: Game, finishQuirk = true): number[] {
  if (!game.awaitingMove || game.finished) return [];
  const s = read(game, finishQuirk);
  return [0, 1, 2, 3].filter(piece => mayMove(s, s.pieces[piece]));
}

function read(game: Game, finishQuirk: boolean): Position {
  const stride = game.trackLength / game.playerCount, me = game.player;
  const occupant = new Array<number>(game.trackLength + 1).fill(0);
  for (let p = 0; p < game.playerCount; p++)
    if (game.kinds[p] !== Kind.Inactive)
      for (let i = 0; i < 4; i++) {
        const field = encode(game, p, i);
        if (field <= game.trackLength) occupant[field] = p + 1;
      }
  const pieces = [0, 1, 2, 3].map(i => encode(game, me, i));
  const s: Position = {
    game, me: me + 1, die: game.die, trackLength: game.trackLength, start: me * stride + 1,
    finish: me === 0 ? game.trackLength : me * stride, occupant, pieces, finishQuirk, rules: game.rules,
    goal: [0, 1, 2, 3, 4].map(slot => pieces.includes(200 + slot)),
    yard: [0, 1, 2, 3, 4].map(slot => pieces.includes(100 + slot)),
    canEnter: false, anyYard: false, anyCapture: false,
  };
  // seg13:1247..12e4
  s.canEnter = game.die === 6 || (game.die === 1 && game.rules.enterOnOne);
  s.anyCapture = pieces.some(field => canCapture(s, field));
  s.anyYard = s.yard.slice(1).some(Boolean);
  return s;
}

const inYard = (f: number) => f >= 101 && f <= 104;
const inGoal = (f: number) => f >= 201 && f <= 204;
const wrap = (s: Position, f: number) => f > s.trackLength ? f - s.trackLength : f;   // seg07:03a5
const isRest = (s: Position, f: number) => s.game.isRestSquare(f - 1);
const isOpponent = (s: Position, p: number) => p !== 0 && p !== s.me;

// seg13:1140
function mayMove(s: Position, field: number) {
  if (field === 0) return false;
  if (inYard(field) && !s.yard[field - 100]) return false;
  if (inGoal(field) && !s.goal[field - 200]) return false;
  if (field >= 1 && field <= s.trackLength && s.occupant[field] !== s.me) return false;
  return decide(s, field);
}

// seg13:0e32 mit der Grenze des Originals: eine Figur genau auf ihrem letzten Bahnfeld
// gilt hier nicht als "ins Ziel", daher zählt Wrap(finish + die) als Schlagziel.
function canCapture(s: Position, field: number) {
  let result = false, decided = false;
  if (field >= 1 && field <= s.trackLength && !((s.finishQuirk ? s.finish > field : s.finish >= field) && field + s.die > s.finish)) {
    const target = wrap(s, field + s.die);
    if (isOpponent(s, s.occupant[target]) && !(s.rules.rest && isRest(s, target))) { result = true; decided = true; }
  }
  if (!decided) result = inYard(field) && s.canEnter && isOpponent(s, s.occupant[s.start]);
  if (result && !inYard(field) && s.rules.noJump)
    for (let step = 1; step <= s.die; step++)
      if (s.occupant[wrap(s, field + step)] === s.me) result = false;
  return result;
}

// seg13:0012
const overshoots = (s: Position, field: number) => inGoal(field) ? field + s.die > 204 : field + s.die - s.finish > 4;

function goalSlot(s: Position, slot: number) {
  if (slot < 1 || slot > 4) throw new Error('Zielfeld außerhalb der Originalprüfung.');
  return s.goal[slot];
}

// seg13:00da
function goalPathFree(s: Position, field: number) {
  const first = inGoal(field) ? field - 200 + 1 : 1;
  const last = inGoal(field) ? field - 200 + s.die : field + s.die - s.finish;
  for (let slot = first; slot <= last; slot++) if (goalSlot(s, slot)) return false;
  return true;
}

// seg13:0244
function pathBlocked(s: Position, field: number) {
  if (inYard(field)) return false;
  if (inGoal(field)) return !goalPathFree(s, field);
  let blocked = false, goal = s.finish === field;
  for (let step = 1; step <= s.die; step++) {
    if (goal ? goalSlot(s, field + step - s.finish) : s.occupant[wrap(s, field + step)] === s.me) blocked = true;
    if (field + step === s.finish) goal = true;
  }
  return blocked;
}

// seg13:043d
function decide(s: Position, field: number): boolean {
  const r = s.rules;
  const start = s.occupant[s.start];
  const ownStart = start === s.me;
  const startChain = (f: number) => s.start === f || wrap(s, s.start + s.die) === f ||
    (s.occupant[wrap(s, s.start + s.die)] === s.me && wrap(s, s.start + 2 * s.die) === f);
  let state = 0;
  for (;;) {
    switch (state) {
      case 0: state = inYard(field) ? 1 : inGoal(field) || (s.finish >= field && field + s.die > s.finish) ? 4 : 5; break;
      case 1: state = (s.die >= 2 && s.die <= 5) || (s.die === 1 && !r.enterOnOne) ? Illegal : 2; break;
      case 2: state = ownStart ? Illegal : 3; break;
      case 3: state = start === 0 && r.capture && r.absolute && s.anyCapture ? Illegal : Legal; break;
      case 4:
        state = overshoots(s, field) || (r.noJump && pathBlocked(s, field)) || !goalPathFree(s, field) ||
          (s.anyCapture && r.capture) || (s.anyYard && s.canEnter) || (s.anyYard && ownStart) ? Illegal : Legal;
        break;
      case 5: {
        const target = wrap(s, field + s.die), occupant = s.occupant[target];
        if (occupant === s.me || (r.rest && isRest(s, target) && occupant !== 0) || (r.noJump && pathBlocked(s, field))) state = Illegal;
        else state = occupant !== 0 ? 6 : 12;
        break;
      }
      case 6:
        if (s.canEnter && s.anyYard && isOpponent(s, start)) state = 19;
        else if (s.canEnter && s.anyYard && start === 0 && !(r.capture && r.absolute)) state = Illegal;
        else if (s.anyYard && ownStart) state = 7;
        else state = 11;
        break;
      case 7: case 8: case 9: state = startChain(field) ? 11 : 10; break;
      case 10: state = r.capture && (r.hard || r.absolute) ? 11 : Illegal; break;
      case 11: case 17: state = r.noJump && pathBlocked(s, field) ? Illegal : Legal; break;
      case 12: state = r.capture && (r.hard || r.absolute) && s.anyCapture ? Illegal : 13; break;
      case 13: state = s.anyYard && ownStart ? 14 : 18; break;
      case 14: case 15: case 16: state = startChain(field) ? 17 : Illegal; break;
      case 18: state = (s.anyYard && s.canEnter) || (r.capture && s.anyCapture) ? Illegal : 17; break;
      case 19: state = r.capture ? Legal : Illegal; break;
      case Illegal: return false;
      case Legal: return true;
      default: throw new Error(`Unbekannter Zustand ${state}.`);
    }
  }
}
