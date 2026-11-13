namespace Aerger;

/// <summary>Ablage für Screenshots und Protokolle der automatischen EXE-Prüfungen,
/// getrennt vom auszuliefernden Paket.</summary>
internal static class TestOutput
{
    internal static string PathFor(string name)
    {
        string directory=Path.Combine(AppContext.BaseDirectory,"test-output");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,name);
    }
}
