using System.Text;

namespace Aerger;

// Win16 file layout: segment 12, 0002 (write) and 0407 (read).
public static class OriginalSave
{
    private static readonly Encoding TextEncoding;
    static OriginalSave()
    { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); TextEncoding = Encoding.GetEncoding(1252); }

    public static Game Read(byte[] bytes)
    {
        void Require(bool valid) { if (!valid) throw new InvalidDataException("Ungültiger ERG-Spielstand."); }
        Require(bytes.Length == 331);
        Require(bytes[138] is 4 or 6 && bytes[139] == (bytes[138] == 6 ? 1 : 0));
        Require(bytes.AsSpan(140, 7).ToArray().All(v => v <= 1));
        Require(bytes[327] <= 1 && bytes[328] <= 6 && bytes[329] <= 3 && bytes[330] >= 1 && bytes[330] <= bytes[138]);
        Require(bytes[327] == 0 || bytes[328] > 0);
        var g = new Game(bytes[138], bytes[330] - 1) {
            Rules = new(bytes[140]!=0,bytes[141]!=0,bytes[142]!=0,bytes[143]!=0,bytes[144]!=0,bytes[145]!=0,bytes[146]!=0)
        };
        var occupied = new HashSet<int>();
        for (int p = 0; p < 6; p++)
        {
            int o = p * 23;
            Require(bytes[o] <= 21 && bytes[o + 22] <= 2);
            g.Names[p] = TextEncoding.GetString(bytes, o + 1, bytes[o]);
            g.Kinds[p] = (PlayerKind)bytes[o + 22];
            var own = new HashSet<int>();
            for (int i = 0; i < 4; i++)
            {
                int field = bytes[147 + p * 4 + i];
                // Pieces of inactive players and of players 5/6 on the four board are never drawn
                // or read (seg17:0405). The original leaves them stale: seg07:042c keeps players
                // 5/6 on fields up to 48, seg07:0584 puts inactive players 5/6 in their yards.
                if (p >= g.PlayerCount || g.Kinds[p] == PlayerKind.Inactive)
                {
                    Require(field is 0 or >= 1 and <= 48 || field == 101 + i || field is >= 201 and <= 204);
                    g.SetPosition(p, i, -1);
                    continue;
                }
                Require(field == 101 + i || field is >= 201 and <= 204 || field >= 1 && field <= g.TrackLength);
                int position = field >= 101 && field <= 104 ? -1 : field >= 201 ? g.TrackLength + field - 201 : (field - 1 - p * (g.TrackLength / g.PlayerCount) + g.TrackLength) % g.TrackLength;
                g.SetPosition(p, i, position);
                if (position < 0) continue;
                Require(own.Add(position));
                if (position < g.TrackLength) Require(occupied.Add(field));
            }
        }
        foreach (byte winner in bytes.AsSpan(321, 6))
        {
            if (winner == 0) continue;
            Require(winner <= 6 && !g.Winners.Contains(winner - 1));
            g.Winners.Add(winner - 1);
        }
        g.RestoreTurn(bytes[330] - 1, bytes[328], bytes[327] != 0, bytes[329]);
        return g;
    }

    public static byte[] Write(Game g)
    {
        byte[] b = new byte[331];
        for (int p = 0; p < 6; p++)
        {
            byte[] name = TextEncoding.GetBytes(g.Names[p]);
            int length = Math.Min(21, name.Length), o = p * 23;
            b[o] = (byte)length; Array.Copy(name, 0, b, o + 1, length);
            b[o + 22] = (byte)g.Kinds[p];
            for (int i = 0; i < 4; i++)
            {
                int at = g.Position(p, i), n = p * 4 + i;
                // seg04:01ca clears disabled pieces to zero, including yard
                // and goal flags. Their return initializes fresh yard slots.
                if (g.Kinds[p] == PlayerKind.Inactive) continue;
                b[147 + n] = (byte)(at < 0 ? 101 + i : at >= g.TrackLength ? 201 + at - g.TrackLength : g.Square(p, at) + 1);
                b[267 + n] = (byte)(at < 0 ? 1 : 0);
                if (at >= g.TrackLength) b[291 + p * 4 + at - g.TrackLength] = (byte)(i + 1);
                if (at >= 0 && at < g.TrackLength && p < g.PlayerCount && g.Kinds[p] != PlayerKind.Inactive)
                { int field = g.Square(p, at); b[171 + field * 2] = (byte)(p + 1); b[172 + field * 2] = (byte)(i + 1); }
            }
        }
        b[138] = (byte)g.PlayerCount; b[139] = (byte)(g.PlayerCount == 6 ? 1 : 0);
        bool[] rules = [g.Rules.Rest,g.Rules.Capture,g.Rules.Hard,g.Rules.Absolute,g.Rules.NoJump,g.Rules.EnterOnOne,g.Rules.RepeatOnOne];
        for (int i = 0; i < 7; i++) b[140 + i] = (byte)(rules[i] ? 1 : 0);
        for (int i = 0; i < g.Winners.Count; i++) { b[315 + g.Winners[i]] = 1; b[321 + i] = (byte)(g.Winners[i] + 1); }
        b[327] = (byte)(g.AwaitingMove ? 1 : 0); b[328] = (byte)g.Die; b[329] = (byte)g.Attempts; b[330] = (byte)(g.Player + 1);
        return b;
    }
}
