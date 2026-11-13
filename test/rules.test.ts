// Regeltests, portiert aus der Paritätsversion (tests/Program.cs im Branch original-paritaet).
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { readErg, writeErg, InvalidSaveError } from '../src/core/erg';
import { Game, Kind, rules, type Rules } from '../src/core/game';
import { OriginalRandom } from '../src/core/random';
import { chooseMove, chooseOriginal, originalScore } from '../src/core/strategy';
import { originalLegalMoves } from './originalMoveCheck';

const exampleBytes = new Uint8Array(readFileSync(new URL('./fixtures/BEISPIEL.ERG', import.meta.url)));

/** Reproduzierbarer Zufall für Testpartien (mulberry32). */
function seeded(seed: number) {
  let a = seed >>> 0;
  const next = () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  return { next, int: (min: number, max: number) => min + Math.floor(next() * (max - min)), bool: () => next() < 0.5 };
}
const randomRules = (r: ReturnType<typeof seeded>): Rules => rules({
  rest: r.bool(), capture: r.bool(), hard: r.bool(), absolute: r.bool(), noJump: r.bool(), enterOnOne: r.bool(), repeatOnOne: r.bool(),
});

function position(own: number[], die: number, r: Partial<Rules> = {}, ...opponents: [number, number, number][]) {
  const state = new Game();
  state.rules = rules(r);
  own.forEach((at, i) => state.setPosition(0, i, at));
  for (const [p, i, d] of opponents) state.setPosition(p, i, d);
  state.roll(die);
  return state;
}
const moves = (state: Game) => state.legalMoves();
/** Schnelle Prüfung für heiße Schleifen (expect ist dort zu langsam). */
function check(condition: boolean, message: string) { if (!condition) throw new Error(message); }
const range = (n: number, from = 0) => Array.from({ length: n }, (_, i) => from + i);

describe('ERG-Spielstände', () => {
  it('liest BEISPIEL.ERG und schreibt alle Zustandsbytes identisch zurück', () => {
    const example = readErg(exampleBytes);
    expect([example.playerCount, example.player, example.die, example.awaitingMove]).toEqual([4, 1, 2, false]);
    expect([example.position(0, 3), example.position(1, 0), example.position(2, 2)]).toEqual([41, 43, 3]);
    expect([example.kinds[0], example.kinds[4]]).toEqual([Kind.Human, Kind.Computer]);
    const exported = writeErg(example);
    expect(exported.subarray(138)).toEqual(exampleBytes.subarray(138));
    for (let p = 0; p < 6; p++)
      expect(exported.subarray(p * 23, p * 23 + exampleBytes[p * 23] + 1)).toEqual(exampleBytes.subarray(p * 23, p * 23 + exampleBytes[p * 23] + 1));
  });

  it('weist beschädigte Dateien zurück', () => {
    expect(() => readErg(exampleBytes.subarray(0, 330))).toThrow(InvalidSaveError);
    expect(() => readErg(new Uint8Array(331))).toThrow(InvalidSaveError);
  });

  it('lädt Altdaten der Spieler 5/6 auf dem Viererbrett (seg07:042c/0584)', () => {
    const stale = exampleBytes.slice();
    stale[4 * 23 + 22] = Kind.Computer; stale[147 + 16] = 45; stale[147 + 17] = 12;
    stale[5 * 23 + 22] = Kind.Inactive; stale[147 + 20] = 101;
    const loaded = readErg(stale);
    expect([loaded.playerCount, loaded.position(4, 0), loaded.position(5, 0)]).toEqual([4, -1, -1]);
    expect(loaded.changeBoard(6)).toBe(true);
    for (const p of [4, 5]) for (const i of range(4)) expect(loaded.position(p, i)).toBe(-1);
  });

  it('erhält Brettwechsel, Wurf und Windows-1252-Namen', () => {
    const exported = writeErg(readErg(exampleBytes));
    const switched = readErg(exampleBytes);
    expect(switched.changeBoard(6)).toBe(true);
    expect(switched.position(0, 3)).toBe(49);
    expect(switched.square(1, switched.position(1, 1))).toBe(29);
    expect(switched.changeBoard(4)).toBe(true);
    expect(writeErg(switched).subarray(138)).toEqual(exported.subarray(138));
    const blocked = new Game(6);
    blocked.setPosition(0, 0, 40);
    expect(blocked.changeBoard(4)).toBe(false);
    expect([blocked.playerCount, blocked.position(0, 0)]).toEqual([6, 40]);
    const saved = new Game(6); saved.rules = rules({ enterOnOne: true });
    saved.names[0] = 'Ärger Ö ß €';
    saved.roll(1);
    const reloaded = readErg(writeErg(saved));
    expect(reloaded.awaitingMove && reloaded.attempts === 1).toBe(true);
    expect(reloaded.legalMoves()).toEqual(saved.legalMoves());
    expect(reloaded.names[0]).toBe(saved.names[0]);
  });
});

describe('Grundregeln', () => {
  it('drei Startversuche, Sechs startet, eigene Figur blockiert das Startfeld', () => {
    const game = new Game();
    for (let attempt = 0; attempt < 3; attempt++) {
      game.roll(2);
      expect(game.legalMoves()).toEqual([]);
      game.pass();
      expect(game.player).toBe(attempt === 2 ? 1 : 0);
    }
    game.roll(6);
    expect(game.legalMoves()).toHaveLength(4);
    game.move(0);
    expect(game.player).toBe(1);
    expect(game.position(1, 0)).toBe(0);
    game.roll(6);
    expect(game.legalMoves()).toEqual([0]);
    game.move(0);
  });

  it('30 vollständige Zufallspartien enden regelkonform', () => {
    let captures = 0;
    for (let seed = 0; seed < 30; seed++) {
      const game = new Game(), r = seeded(seed);
      for (let turn = 0; turn < 20000 && game.winner < 0; turn++) {
        game.roll(r.int(1, 7));
        const legal = game.legalMoves();
        if (legal.length === 0) game.pass();
        else if (game.move(legal[r.int(0, legal.length)])) captures++;
        const occupied = new Set<number>();
        for (let p = 0; p < 4; p++) {
          const home = new Set<number>();
          for (let i = 0; i < 4; i++) {
            const at = game.position(p, i);
            check(at >= -1 && at <= 43, 'Figur außerhalb des Bretts.');
            if (at >= 0 && at < 40) { check(!occupied.has(game.square(p, at)), 'Doppelt belegtes Feld.'); occupied.add(game.square(p, at)); }
            if (at >= 40) { check(!home.has(at), 'Doppelt belegtes Zielfeld.'); home.add(at); }
          }
        }
      }
      expect(game.winner).toBeGreaterThanOrEqual(0);
      for (const i of range(4)) expect(game.position(game.winner, i)).toBeGreaterThanOrEqual(40);
    }
    expect(captures).toBeGreaterThan(0);
  });

  it('Würfelgenerator identisch mit seg25:0fbd/101a/1052', () => {
    const zero = new OriginalRandom(0);
    expect(range(12).map(() => zero.next(6))).toEqual([0, 0, 5, 1, 1, 4, 1, 0, 2, 2, 0, 2]);
    const other = new OriginalRandom(0x12345678);
    expect(range(12).map(() => other.next(6))).toEqual([4, 2, 5, 5, 5, 5, 3, 4, 2, 5, 4, 0]);
    expect(OriginalRandom.seedFromClock(new Date(2026, 10, 3, 13, 45, 7, 890))).toBe(((7 << 24) | (89 << 16) | (13 << 8) | 45) >>> 0);
  });

  it.each([4, 6] as const)('drei Startversuche nur bei lückenlos belegten hintersten Zielfeldern (%i Spieler)', players => {
    for (const goals of [0, 1, 2, 3]) {
      let packed = new Game(players);
      for (let i = 0; i < goals; i++) packed.setPosition(0, i, packed.trackLength + 3 - i);
      for (let attempt = 1; attempt <= 3; attempt++) {
        packed.roll(5);
        expect(packed.legalMoves()).toEqual([]);
        packed.pass();
        expect([packed.player, packed.attempts]).toEqual([attempt === 3 ? 1 : 0, attempt === 3 ? 0 : attempt]);
        packed = readErg(writeErg(packed));
      }
    }
    for (const slots of [[0], [1], [2], [0, 3], [1, 3], [0, 2, 3]]) {
      const gap = new Game(players);
      slots.forEach((slot, i) => gap.setPosition(0, i, gap.trackLength + slot));
      gap.roll(5);
      expect(gap.legalMoves()).toEqual([]);
      gap.pass();
      expect([gap.player, gap.awaitingMove, gap.attempts]).toEqual([1, false, 0]);
    }
    let loadedGap = new Game(players);
    loadedGap.setPosition(0, 0, loadedGap.trackLength); loadedGap.roll(5);
    loadedGap.kinds[1] = Kind.Inactive; loadedGap.winners.push(2);
    loadedGap = readErg(writeErg(loadedGap)); loadedGap.reconcileTurn();
    expect([loadedGap.player, loadedGap.awaitingMove, loadedGap.attempts]).toEqual([3, false, 0]);
    const canEnter = new Game(players); canEnter.rules = rules({ enterOnOne: true });
    canEnter.setPosition(0, 0, canEnter.trackLength);
    canEnter.roll(1); canEnter.move(1);
    expect(canEnter.player).toBe(1);
    const repeatAttempt = new Game(players); repeatAttempt.rules = rules({ repeatOnOne: true });
    const t = repeatAttempt.trackLength;
    repeatAttempt.setPosition(0, 0, t + 3); repeatAttempt.setPosition(0, 1, t + 2); repeatAttempt.setPosition(0, 2, t + 1);
    repeatAttempt.roll(1); expect(repeatAttempt.legalMoves()).toEqual([]);
    repeatAttempt.pass(); expect(repeatAttempt.player).toBe(0);
    repeatAttempt.roll(1); repeatAttempt.pass();
    repeatAttempt.roll(1); repeatAttempt.pass();
    expect([repeatAttempt.player, repeatAttempt.attempts]).toEqual([1, 0]);
    const ordinaryRepeat = new Game(players); ordinaryRepeat.rules = rules({ repeatOnOne: true, noJump: true });
    ordinaryRepeat.setPosition(0, 0, t + 2); ordinaryRepeat.setPosition(0, 1, t + 3);
    ordinaryRepeat.setPosition(0, 2, 0); ordinaryRepeat.setPosition(0, 3, 1);
    ordinaryRepeat.roll(1); ordinaryRepeat.move(3);
    expect([ordinaryRepeat.player, ordinaryRepeat.attempts]).toEqual([0, 0]);
  });
});

describe('Regressionsfälle aus der Originalhilfe', () => {
  it('Raussetzen und Startfeld räumen haben Vorrang', () => {
    expect(moves(position([5, -1, -1, -1], 6))).toEqual([1, 2, 3]);
    expect(moves(position([0, 6, 12, -1], 6))).toEqual([2]);
    expect(moves(position([0, 10, -1, -1], 3))).toEqual([0]);
  });

  it('unterscheidet die drei Schmeißzwang-Varianten', () => {
    const enemy: [number, number, number] = [1, 0, 3];
    expect(moves(position([0, 10, -1, -1], 3, { capture: true }, enemy))).toEqual([0]);
    expect(moves(position([0, 10, -1, -1], 3, { capture: true, hard: true }, enemy))).toEqual([1]);
    expect(moves(position([0, 10, -1, -1], 3, { capture: true, absolute: true }, enemy))).toEqual([1]);
    expect(moves(position([7, -1, -1, -1], 6, { capture: true, hard: true }, enemy))).toEqual([1, 2, 3]);
    expect(moves(position([7, -1, -1, -1], 6, { capture: true, absolute: true }, enemy))).toEqual([0]);
  });

  it('Ruhefelder, Überspringen, Ziel, Einser-Regeln, Rangliste', () => {
    expect(moves(position([0, 10, -1, -1], 4, { rest: true }, [1, 0, 34]))).toEqual([]);
    expect(moves(position([0, 10, -1, -1], 5, { rest: true }, [1, 0, 34]))).toEqual([0]);
    expect(moves(position([5, 7, 20, 30], 3, { noJump: true }))).toEqual([1, 2, 3]);
    expect(moves(position([38, 40, 42, 43], 3))).toEqual([]);
    expect(moves(position([39, 41, 42, 43], 1))).toEqual([0]);
    expect(moves(position([-1, -1, -1, -1], 1, { enterOnOne: true }))).toEqual([0, 1, 2, 3]);
    const repeat = position([5, 10, 20, 30], 1, { repeatOnOne: true });
    repeat.move(0);
    expect(repeat.player).toBe(0);
    const trials = position([-1, 41, 42, 43], 5);
    trials.pass();
    expect(trials.player).toBe(0);
    const finish = position([39, 41, 42, 43], 1);
    finish.move(0);
    expect([finish.winners, finish.player, finish.finished]).toEqual([[0], 1, false]);
    const two = position([39, 41, 42, 43], 1);
    two.kinds[2] = two.kinds[3] = Kind.Inactive;
    two.move(0);
    expect([two.finished, two.winners, two.position(1, 0)]).toEqual([true, [0, 1], -1]);
  });

  it('Originalstrategie seg06', () => {
    const strategyCase = position([40, 10, 20, 30], 1, {}, [1, 0, 1]);
    expect(originalScore(strategyCase, 0)).toBe(-3);
    expect(chooseOriginal(strategyCase)).toBe(0);
    expect(chooseOriginal(position([5, 12, 22, 27], 1))).toBe(0);
    expect(range(40).filter(s => new Game(4).isRestSquare(s))).toEqual([4, 14, 24, 34]);
    expect(range(48).filter(s => new Game(6).isRestSquare(s))).toEqual([3, 11, 19, 27, 35, 43]);
    const sixRest = (die: number, enemy: number) => {
      const s = new Game(6); s.rules = rules({ rest: true });
      s.setPosition(0, 0, 0);
      if (enemy >= 0) s.setPosition(1, 0, enemy);
      s.roll(die);
      return s;
    };
    expect(moves(sixRest(3, 43))).toEqual([]);
    expect(moves(sixRest(4, 44))).toEqual([0]);
    expect([originalScore(sixRest(3, -1), 0), originalScore(sixRest(4, -1), 0)]).toEqual([-1, 0]);
    expect(originalScore(position([0, -1, -1, -1], 4, { rest: true }), 0)).toBe(-1);
    expect(originalScore(position([0, -1, -1, -1], 3, { rest: true }), 0)).toBe(0);
    const leaveRest = new Game(6); leaveRest.rules = rules({ rest: true });
    leaveRest.setPosition(0, 0, 3); leaveRest.roll(2);
    expect(originalScore(leaveRest, 0)).toBe(1);
  });
});

describe('Zugabgleich nach Änderungen', () => {
  it('Regelwechsel, Spielertyp, Deaktivierung, Laden, Brettwechsel', () => {
    const changedRules = position([5, 7, 20, 30], 3);
    changedRules.rules = rules({ noJump: true });
    changedRules.reconcileTurn();
    expect([changedRules.player, changedRules.awaitingMove, changedRules.die, changedRules.attempts]).toEqual([0, true, 3, 1]);
    expect(moves(changedRules)).toEqual([1, 2, 3]);
    const lostEntry = position([-1, -1, -1, -1], 1, { enterOnOne: true });
    lostEntry.rules = rules();
    lostEntry.reconcileTurn();
    expect([lostEntry.awaitingMove, lostEntry.player, lostEntry.attempts]).toEqual([false, 0, 1]);
    const settled = writeErg(lostEntry);
    lostEntry.reconcileTurn();
    expect(writeErg(lostEntry)).toEqual(settled);
    lostEntry.roll(2); lostEntry.reconcileTurn();
    lostEntry.roll(2); lostEntry.reconcileTurn();
    expect([lostEntry.player, lostEntry.awaitingMove, lostEntry.attempts]).toEqual([1, false, 0]);
    const changedKind = position([5, 10, 20, 30], 2);
    changedKind.setPlayerKind(0, Kind.Computer);
    changedKind.reconcileTurn();
    expect([changedKind.player, changedKind.awaitingMove, changedKind.die, changedKind.attempts]).toEqual([0, true, 2, 1]);
    changedKind.setPlayerKind(0, Kind.Inactive);
    changedKind.reconcileTurn();
    expect([changedKind.player, changedKind.awaitingMove, changedKind.attempts]).toEqual([1, false, 0]);
    const inactiveSave = position([5, 10, 20, 30], 2);
    inactiveSave.kinds[0] = Kind.Inactive;
    const inactiveReload = readErg(writeErg(inactiveSave));
    inactiveReload.reconcileTurn();
    expect([inactiveReload.player, inactiveReload.awaitingMove, inactiveReload.attempts]).toEqual([1, false, 0]);
    const noPlayers = position([5, 10, 20, 30], 2);
    noPlayers.kinds.fill(Kind.Inactive);
    noPlayers.reconcileTurn();
    expect([noPlayers.finished, noPlayers.awaitingMove, noPlayers.attempts]).toEqual([true, false, 0]);
    noPlayers.kinds[2] = Kind.Human;
    noPlayers.reconcileTurn();
    expect([noPlayers.finished, noPlayers.player, noPlayers.awaitingMove]).toEqual([false, 2, false]);
    noPlayers.roll(6); noPlayers.move(0);
    expect(noPlayers.position(2, 0)).toBe(0);
    const ranked = position([40, 41, 42, 43], 1);
    ranked.winners.push(0);
    ranked.reconcileTurn();
    expect([ranked.player, ranked.awaitingMove, ranked.attempts]).toEqual([1, false, 0]);
    const removed = new Game(6, 5);
    removed.roll(6);
    expect(removed.changeBoard(4)).toBe(true);
    removed.reconcileTurn();
    expect([removed.player, removed.awaitingMove, removed.attempts]).toEqual([0, false, 0]);
  });

  it.each([4, 6] as const)('Spieler entfernen und neu einsetzen (%i Spieler)', players => {
    const lifecycle = new Game(players);
    const target = lifecycle.trackLength / players + 5;
    lifecycle.setPosition(0, 0, target - 2);
    lifecycle.setPosition(1, 0, 5);
    lifecycle.setPosition(1, 1, lifecycle.trackLength + 1);
    lifecycle.setPosition(1, 2, lifecycle.trackLength + 3);
    lifecycle.roll(2);
    const beforeKind = writeErg(lifecycle);
    lifecycle.setPlayerKind(1, Kind.Computer);
    lifecycle.setPlayerKind(1, Kind.Human);
    expect(writeErg(lifecycle)).toEqual(beforeKind);
    lifecycle.setPlayerKind(1, Kind.Inactive);
    for (const i of range(4)) expect(lifecycle.position(1, i)).toBe(-1);
    const disabled = writeErg(lifecycle);
    expect([...disabled.subarray(151, 155), ...disabled.subarray(271, 275), ...disabled.subarray(295, 299), disabled[171 + target * 2]]).toEqual(new Array(13).fill(0));
    expect(writeErg(readErg(disabled))).toEqual(disabled);
    expect(lifecycle.move(0)).toBe(false);
    expect(lifecycle.position(0, 0)).toBe(target);
    lifecycle.setPlayerKind(1, Kind.Human);
    for (const i of range(4)) expect(lifecycle.position(1, i)).toBe(-1);
    expect([lifecycle.player, lifecycle.awaitingMove]).toEqual([2, false]);
    const reactivated = writeErg(lifecycle);
    expect([...reactivated.subarray(151, 155)]).toEqual([101, 102, 103, 104]);
    expect([...reactivated.subarray(271, 275)]).toEqual([1, 1, 1, 1]);
    expect(writeErg(readErg(reactivated))).toEqual(reactivated);
    const originalDisabled = writeErg(new Game(players));
    originalDisabled[45] = 2;
    originalDisabled.fill(0, 151, 155); originalDisabled.fill(0, 271, 275);
    const zeroFields = readErg(originalDisabled);
    expect(zeroFields.kinds[1]).toBe(Kind.Inactive);
    expect(writeErg(zeroFields)).toEqual(originalDisabled);
    zeroFields.setPlayerKind(1, Kind.Computer);
    for (const i of range(4)) expect(zeroFields.position(1, i)).toBe(-1);
    const legacy = beforeKind.slice(); legacy[45] = 2;
    const legacyGame = readErg(legacy);
    legacyGame.setPlayerKind(1, Kind.Human);
    for (const i of range(4)) expect(legacyGame.position(1, i)).toBe(-1);
    originalDisabled[45] = 0;
    expect(() => readErg(originalDisabled)).toThrow(InvalidSaveError);
  });

  it('vorgemerkte Änderungen gelten erst beim Spielerwechsel bzw. Spielende (seg04:0022/15ae)', () => {
    const deferred = new Game();
    deferred.setPosition(0, 0, 10); deferred.setPosition(1, 0, 5);
    const names = [...deferred.names]; names[0] = 'Neu';
    const kinds = [...deferred.kinds]; kinds[1] = Kind.Inactive; kinds[2] = Kind.Computer;
    deferred.setPendingPlayers(names, kinds);
    deferred.roll(6); deferred.move(1);
    expect([deferred.player, deferred.names[0], deferred.kinds[1], deferred.position(1, 0), deferred.pendingKinds !== null]).toEqual([0, 'Spieler 1', Kind.Human, 5, true]);
    deferred.roll(2); deferred.move(1);
    expect([deferred.names[0], deferred.kinds[1], deferred.kinds[2], deferred.player, deferred.hasPendingChanges]).toEqual(['Neu', Kind.Inactive, Kind.Computer, 2, false]);
    for (const i of range(4)) expect(deferred.position(1, i)).toBe(-1);

    const ruled = new Game(); ruled.rules = rules({ enterOnOne: true }); ruled.pendingRules = rules();
    ruled.roll(1);
    expect(ruled.legalMoves()).toHaveLength(4);
    ruled.move(0);
    expect([ruled.rules.enterOnOne, ruled.pendingRules, ruled.player]).toEqual([false, null, 1]);
    const retry = new Game(); retry.pendingRules = rules({ capture: true });
    retry.roll(3); retry.pass();
    expect([retry.player, retry.pendingRules !== null, retry.rules.capture]).toEqual([0, true, false]);
    const pass = new Game(); pass.pendingRules = rules({ capture: true });
    pass.setPosition(0, 0, 42); pass.roll(2); pass.pass();
    expect([pass.player, pass.rules.capture, pass.pendingRules]).toEqual([1, true, null]);

    const grow = new Game(4); grow.restoreTurn(3, 0, false, 0); grow.setPosition(3, 0, 10); grow.pendingBoard = 6;
    grow.roll(2); grow.move(0);
    expect([grow.playerCount, grow.player, grow.boardChangeRejected]).toEqual([6, 4, false]);
    const shrink = new Game(6); shrink.restoreTurn(5, 0, false, 0); shrink.setPosition(5, 0, 3); shrink.pendingBoard = 4;
    shrink.roll(2); shrink.move(0);
    expect([shrink.playerCount, shrink.player, shrink.position(5, 0)]).toEqual([4, 0, -1]);
    const blocked = new Game(6); blocked.setPosition(0, 0, 41); blocked.pendingBoard = 4;
    blocked.roll(2); blocked.move(0);
    expect([blocked.playerCount, blocked.pendingBoard, blocked.takeBoardChangeRejected(), blocked.takeBoardChangeRejected(), blocked.player, blocked.position(0, 0)])
      .toEqual([6, null, true, false, 1, 43]);

    const ended = new Game(); ended.setPosition(1, 0, 7);
    ended.setPendingPlayers(names, kinds); ended.pendingRules = rules({ rest: true }); ended.pendingBoard = 6;
    expect(ended.applyPendingAfterGame()).toBe(6);
    expect([ended.rules.rest, ended.names[0], ended.kinds[1], ended.position(1, 0), ended.hasPendingChanges, ended.playerCount]).toEqual([true, 'Neu', Kind.Inactive, 7, false, 4]);
  });
});

describe('Abgleich mit der Originalroutine seg13', () => {
  it('20 komplette Sechs-Spieler-Partien mit Sonderregeln', () => {
    for (let seed = 0; seed < 20; seed++) {
      const state = new Game(6);
      state.rules = rules({ rest: true, capture: true, hard: seed % 2 === 0, absolute: seed % 2 === 1, noJump: true, enterOnOne: true, repeatOnOne: true });
      const r = seeded(seed);
      for (let turn = 0; turn < 50000 && !state.finished; turn++) {
        state.roll(r.int(1, 7));
        const legal = state.legalMoves();
        if (legal.length === 0) state.pass(); else state.move(legal[r.int(0, legal.length)]);
        const occupied = new Set<number>();
        for (let p = 0; p < 6; p++)
          for (let i = 0; i < 4; i++) {
            const at = state.position(p, i);
            check(at >= -1 && at <= 51, 'Sechserbrett: Grenzen.');
            if (at >= 0 && at < 48) { check(!occupied.has(state.square(p, at)), 'Sechserbrett: Feldbelegung.'); occupied.add(state.square(p, at)); }
          }
      }
      expect(new Set(state.winners).size).toBe(6);
    }
  });

  it('Schlagen beim Raussetzen erlaubt jeden Schlagzug (seg13:0dc7)', () => {
    expect(moves(position([5, -1, -1, -1], 6, { capture: true }, [3, 0, 10], [1, 0, 1]))).toEqual([0, 1, 2, 3]);
    expect(moves(position([5, -1, -1, -1], 6, { capture: true, hard: true }, [3, 0, 10], [1, 0, 1]))).toEqual([0, 1, 2, 3]);
    expect(moves(position([5, -1, -1, -1], 6, {}, [3, 0, 10], [1, 0, 1]))).toEqual([1, 2, 3]);
    expect(moves(position([5, -1, -1, -1], 6, { capture: true }, [1, 0, 1]))).toEqual([1, 2, 3]);
  });

  it('Randfall seg13:0e32 (Figur auf dem letzten Bahnfeld) wird bewusst nicht nachgebildet', () => {
    const phantom = position([39, 5, -1, -1], 2, { capture: true }, [3, 0, 11]);
    expect(phantom.legalMoves()).toEqual([0, 1]);
    expect(originalLegalMoves(phantom)).toEqual([]);
    expect(originalLegalMoves(phantom, false)).toEqual([0, 1]);
  });

  it('identisch mit seg13:1140/043d/0e32 in 200000 Stellungen', () => {
    const r = seeded(20261102);
    let quirk = 0;
    for (let sample = 0; sample < 200_000; sample++) {
      const players = r.bool() ? 4 : 6;
      const state = new Game(players);
      state.rules = randomRules(r);
      const track = state.trackLength, stride = track / players, me = r.int(0, players), die = r.int(1, 7);
      const used = new Set<number>();
      for (let p = 0; p < players; p++) {
        if (p !== me && r.int(0, 5) === 0) { state.kinds[p] = Kind.Inactive; continue; }
        const usedGoal = new Set<number>();
        for (let i = 0; i < 4; i++) {
          const kind = r.int(0, 10);
          if (kind < 3) continue;
          if (kind < 5) { const slot = r.int(0, 4); if (!usedGoal.has(slot)) { usedGoal.add(slot); state.setPosition(p, i, track + slot); } continue; }
          let distance = r.int(0, track);
          if (r.bool()) {
            const hot = p === me ? [0, die, 2 * die, 3 * die, track - 1, track - 2, track - die] : [0, die, 2 * die, 3 * die, 3, 4];
            distance = (me * stride + hot[r.int(0, hot.length)] - p * stride + 2 * track) % track;
          }
          const sq = state.square(p, distance);
          if (!used.has(sq)) { used.add(sq); state.setPosition(p, i, distance); }
        }
      }
      state.restoreTurn(me, die, true, 1);
      const native = state.legalMoves();
      check(native.join() === originalLegalMoves(state, false).join(), `Zugprüfung weicht von seg13:043d ab: ${players} Spieler, Spieler ${me + 1}, Wurf ${die}, ${JSON.stringify(state.rules)}`);
      if (JSON.stringify(native) !== JSON.stringify(originalLegalMoves(state))) {
        quirk++;
        check(state.rules.capture && range(4).some(i => state.position(me, i) === track - 1), 'Abweichung außerhalb des Randfalls seg13:0e32.');
      }
    }
    expect(quirk).toBeGreaterThan(0);
  });
});

describe('Neue Funktionen', () => {
  it('Snapshot und Wiederherstellung sind verlustfrei (Rückgängig, Autosave)', () => {
    const g = readErg(exampleBytes);
    g.pendingRules = rules({ rest: true });
    g.roll(6);
    const copy = Game.restore(JSON.parse(JSON.stringify(g.snapshot())));
    expect(copy.snapshot()).toEqual(g.snapshot());
    expect(copy.legalMoves()).toEqual(g.legalMoves());
    copy.move(copy.legalMoves()[0]);
    expect(g.awaitingMove).toBe(true);   // Original unverändert
  });

  it('Zusatzregel: Sechs ohne Zusatzwurf', () => {
    const g = position([5, -1, -1, -1], 6, { sixBonus: false });
    g.move(1);
    expect(g.player).toBe(1);
  });

  it('Zusatzregel: nach drei Sechsen ist Schluss', () => {
    const g = new Game(); g.rules = rules({ threeSixes: true });
    g.roll(6); g.move(0);           // raus
    g.roll(6); g.move(0);           // 6
    expect(g.player).toBe(0);
    g.roll(6); g.move(1);           // dritte Sechs: Raussetzen ist Pflicht
    expect(g.player).toBe(1);
  });

  it('Zusatzregel: Raussetzen und Startfeld räumen sind abschaltbar', () => {
    expect(moves(position([5, -1, -1, -1], 6, { forcedEntry: false }))).toEqual([0, 1, 2, 3]);
    expect(moves(position([0, 10, -1, -1], 3, { clearStart: false }))).toEqual([0, 1]);
  });

  it('Zusatzregel: Spiel endet mit dem ersten Sieger', () => {
    const g = position([39, 41, 42, 43], 1, { firstWinnerEnds: true });
    g.move(0);
    expect([g.finished, g.winners]).toEqual([true, [0]]);
  });

  it('alle Computerstärken spielen vollständige Partien', () => {
    for (const strength of ['easy', 'normal', 'hard'] as const) {
      const g = new Game(6), r = seeded(7);
      for (let turn = 0; turn < 50000 && !g.finished; turn++) {
        g.roll(r.int(1, 7));
        if (g.legalMoves().length === 0) g.pass(); else g.move(chooseMove(g, strength, r.next));
      }
      expect(g.finished).toBe(true);
    }
  });

  it('Schwer gewinnt meistens gegen Normal und Leicht', () => {
    let hardWins = 0;
    const games = 200;
    for (let seed = 0; seed < games; seed++) {
      const g = new Game(4, seed % 4), r = seeded(1000 + seed);
      g.rules = rules({ firstWinnerEnds: true });
      g.kinds.fill(Kind.Computer);
      const strengths = ['hard', 'normal', 'easy', 'normal'] as const;
      for (let turn = 0; turn < 50000 && !g.finished; turn++) {
        g.roll(r.int(1, 7));
        if (g.legalMoves().length === 0) g.pass(); else g.move(chooseMove(g, strengths[g.player], r.next));
      }
      if (g.winner === 0) hardWins++;
    }
    expect(hardWins / games).toBeGreaterThan(0.3);   // Zufall wäre 0,25
  });
});
