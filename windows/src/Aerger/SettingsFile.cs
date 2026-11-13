namespace Aerger;

/// <summary>Original-AERGER.CFG: 13 Bytes (7 Regeln, Sprache, Effekte, Tempo Mensch/Computer,
/// Brettgröße 1–3, Brett 0=vier/1=sechs Spieler).</summary>
internal static class SettingsFile
{
    internal const int Length=13;

    internal static bool IsValid(ReadOnlySpan<byte> b)=>
        b.Length==Length&&!b[..9].ContainsAnyExcept((byte)0,(byte)1)&&
        b[9]<=3&&b[10]<=3&&b[11] is >=1 and <=3&&b[12]<=1;

    /// <summary>Liefert die gültigen Einstellungsbytes oder null, wenn die Datei fehlt,
    /// beschädigt oder nicht lesbar ist; das Spiel startet dann mit Standardwerten.</summary>
    internal static byte[]? TryRead(string path)
    {
        try
        {
            var info=new FileInfo(path);
            if(!info.Exists||info.Length!=Length)return null;
            byte[] b=File.ReadAllBytes(path);
            return IsValid(b)?b:null;
        }
        catch(IOException){return null;}
        catch(UnauthorizedAccessException){return null;}
    }
}
