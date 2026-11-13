// Zufallsgenerator der Turbo-Pascal-Laufzeit aus AERGER.EXE: seg25:101a schaltet
// RandSeed als RandSeed * $08088405 + 1 weiter, seg25:0fbd Random(N) liefert das
// High-Word von RandSeed * N, seg25:1052 Randomize setzt ihn aus der DOS-Uhrzeit.
export class OriginalRandom {
  constructor(public seed: number) { this.seed = seed >>> 0; }

  static seedFromClock(now: Date): number {
    return ((((now.getSeconds() << 8) | Math.floor(now.getMilliseconds() / 10)) << 16) | (now.getHours() << 8) | now.getMinutes()) >>> 0;
  }

  next(range: number): number {
    this.seed = (Math.imul(this.seed, 0x08088405) + 1) >>> 0;
    return Number((BigInt(this.seed) * BigInt(range)) >> 32n);
  }
}
