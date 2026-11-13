namespace Aerger;

// Turbo Pascal for Windows runtime random generator as used by AERGER.EXE:
// seg25:101a steps RandSeed (DS:1bca/1bcc) as RandSeed * $08088405 + 1,
// seg25:0fbd Random(N) returns the high word of RandSeed * N, and
// seg25:1052 Randomize (called once at startup, seg02:0ac5) seeds it from the
// DOS time: CX = hour:minute as the low word, DX = second:hundredth as the high word.
public sealed class OriginalRandom(uint seed)
{
    public uint Seed { get; set; } = seed;

    public static uint SeedFromClock(DateTime now) =>
        (uint)((now.Second << 8 | now.Millisecond / 10) << 16 | now.Hour << 8 | now.Minute);

    public int Next(int range)
    {
        Seed = unchecked(Seed * 0x08088405u + 1);
        return (int)((ulong)Seed * (uint)range >> 32);
    }
}
