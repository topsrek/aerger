using System.Media;

namespace Aerger;

internal sealed class GameSound : IDisposable
{
    private readonly SoundPlayer[] players;
    private readonly MemoryStream[] streams;
    // seg10:0279 does not measure the WAV data: it starts the sample with sndPlaySound
    // (SND_ASYNC | SND_NODEFAULT | SND_MEMORY) and sets the end to GetTickCount plus the
    // constant DS:173e[sample]; seg10:0306 waits for that time. Welcome, yard exit, step,
    // winner and Info have 0 ms, so nothing waits for them.
    internal static readonly int[] Durations = [0, 840, 1140, 1930, 0, 0, 0, 0, 530, 510, 380, 380, 310, 410];
    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    private long sampleEndsAt;
    // seg10:0306 waits for the currently playing sample, even when the next
    // sample is suppressed by the speed setting. Muted UI tests keep this clock.
    internal int RemainingMilliseconds => (int)Math.Max(0, sampleEndsAt - Clock.ElapsedMilliseconds);
    internal event Action<int>? SamplePlayed;
    public GameSound(string directory)
    {
        var archive = new Archive(directory, "SOUNDS");
        streams = Enumerable.Range(0, archive.Count).Select(i => new MemoryStream(archive.Read(i))).ToArray();
        players = streams.Select(s => new SoundPlayer(s)).ToArray();
        if (streams.Length != Durations.Length) throw new InvalidDataException("SOUNDS enthält nicht 14 Klänge.");
        foreach (var player in players) player.Load();
    }
    // Original routine seg10:0279 takes one-based sample numbers.
    public int Play(int sample, bool audible = true)
    {
        int duration = Durations[sample - 1];
        if (audible) players[sample - 1].Play();
        sampleEndsAt = Clock.ElapsedMilliseconds + duration;
        SamplePlayed?.Invoke(sample);
        return duration;
    }
    public void Dispose()
    { foreach (var p in players) { p.Stop(); p.Dispose(); } foreach (var s in streams) s.Dispose(); }
}
