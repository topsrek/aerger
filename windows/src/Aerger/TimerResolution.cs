using System.Runtime.InteropServices;

namespace Aerger;

// winmm timeBeginPeriod(1) for the process lifetime; Windows restores the resolution at exit.
internal static class TimerResolution
{
    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);

    internal static void Begin() => timeBeginPeriod(1);
}
