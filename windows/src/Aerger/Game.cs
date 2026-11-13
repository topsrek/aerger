namespace Aerger;

public enum PlayerKind { Human, Computer, Inactive }
public sealed record Rules(bool Rest = false, bool Capture = false, bool Hard = false,
    bool Absolute = false, bool NoJump = false, bool EnterOnOne = false, bool RepeatOnOne = false);

// Rules and their precedence: original ERGWIN.HLP, (text export with personal data removed: assets/original/ERGWIN.redacted-personal-info.txt).
public sealed class Game
{
    private readonly int[,] pieces = new int[6, 4];
    public Rules Rules { get; set; } = new();
    public PlayerKind[] Kinds { get; } = Enumerable.Repeat(PlayerKind.Human, 6).ToArray();
    public string[] Names { get; } = Enumerable.Range(1, 6).Select(p => $"Spieler {p}").ToArray();
    public int PlayerCount { get; private set; }
    public int TrackLength => PlayerCount == 4 ? 40 : 48;
    public int Player { get; private set; }
    public int Die { get; private set; }
    public List<int> Winners { get; } = [];
    public int Winner => Winners.Count == 0 ? -1 : Winners[0];
    public bool Finished => Enumerable.Range(0, PlayerCount).All(p => Kinds[p] == PlayerKind.Inactive || Winners.Contains(p));
    public bool AwaitingMove { get; private set; }
    public int Attempts { get; private set; }
    public int Position(int player, int piece) => pieces[player, piece];
    public int Square(int player, int distance) => (player * (TrackLength / PlayerCount) + distance) % TrackLength;
    // Original rest-field sets DS:1790 (four) and DS:17b0 (six): absolute fields
    // 5/15/25/35 and 4/12/20/28/36/44, i.e. four or three fields after each start.
    // Shared by the move check (seg13:0fde), strategy (seg06:02af/0391) and board (seg17:0338).
    public int RestOffset => PlayerCount == 4 ? 4 : 3;
    public bool IsRestSquare(int square) => square % (TrackLength / PlayerCount) == RestOffset;

    // seg04:0054..028e removes disabled players' pieces and initializes a
    // returning player in the yard. Human/computer changes retain the pieces.
    public void SetPlayerKind(int player, PlayerKind kind)
    {
        if (player < 0 || player >= Kinds.Length) throw new ArgumentOutOfRangeException(nameof(player));
        if (kind is < PlayerKind.Human or > PlayerKind.Inactive) throw new ArgumentOutOfRangeException(nameof(kind));
        if (Kinds[player] != kind && (Kinds[player] == PlayerKind.Inactive || kind == PlayerKind.Inactive))
            for (int piece = 0; piece < 4; piece++) pieces[player, piece] = -1;
        Kinds[player] = kind;
    }

    public Game(int players = 4, int startingPlayer = 0)
    {
        if (players is not (4 or 6)) throw new ArgumentOutOfRangeException(nameof(players));
        if (startingPlayer < 0 || startingPlayer >= players) throw new ArgumentOutOfRangeException(nameof(startingPlayer));
        PlayerCount = players;
        Player = startingPlayer;
        for (int p = 0; p < 6; p++)
            for (int i = 0; i < 4; i++) pieces[p, i] = -1;
    }
    // Während einer Partie ändern Spieler-, Regel- und Brettdialog nicht den Spielstand,
    // sondern merken die Änderung vor (seg08:0522..0553, 0f8d..0fbb, 067b..06aa) und zeigen
    // DLG_SPIELERAENDERUNG. seg04:0032..0332 übernimmt sie beim nächsten Spielerwechsel in
    // der Reihenfolge Spieler, Sechserbrett, Viererbrett, Regeln; seg04:15e3..163b am Spielende.
    public string[]? PendingNames { get; private set; }
    public PlayerKind[]? PendingKinds { get; private set; }
    public Rules? PendingRules { get; set; }
    public int? PendingBoard { get; set; }
    public bool HasPendingChanges => PendingKinds is not null || PendingRules is not null || PendingBoard is not null;
    /// <summary>Ein vorgemerkter Wechsel auf das Viererbrett war beim Spielerwechsel nicht
    /// möglich (seg07:042c zeigt dann DLG_KEINUMSCHALTEN); die Oberfläche meldet es einmal.</summary>
    public bool BoardChangeRejected { get; private set; }
    public void SetPendingPlayers(string[] names, PlayerKind[] kinds)
    {
        if (names.Length != 6 || kinds.Length != 6) throw new ArgumentException("Sechs Spieler erwartet.");
        PendingNames = (string[])names.Clone();
        PendingKinds = (PlayerKind[])kinds.Clone();
    }
    public bool TakeBoardChangeRejected()
    {
        bool rejected = BoardChangeRejected;
        BoardChangeRejected = false;
        return rejected;
    }
    private void ApplyPendingTurnChanges()
    {
        if (PendingKinds is not null)
        {
            for (int p = 0; p < 6; p++) { Names[p] = PendingNames![p]; SetPlayerKind(p, PendingKinds[p]); }
            PendingNames = null; PendingKinds = null;
        }
        if (PendingBoard is int board)
        {
            PendingBoard = null;
            if (!ChangeBoard(board)) BoardChangeRejected = true;
        }
        if (PendingRules is not null) { Rules = PendingRules; PendingRules = null; }
    }
    /// <summary>seg04:15ae: Am Spielende werden alle vorgemerkten Änderungen ohne
    /// Stellungsprüfung übernommen. Liefert das neue Brett oder null.</summary>
    public int? ApplyPendingAfterGame()
    {
        if (PendingRules is not null) Rules = PendingRules;
        if (PendingKinds is not null) { Array.Copy(PendingNames!, Names, 6); Array.Copy(PendingKinds, Kinds, 6); }
        int? board = PendingBoard;
        PendingRules = null; PendingNames = null; PendingKinds = null; PendingBoard = null;
        return board;
    }
    public void Roll(int value)
    {
        if (Finished || AwaitingMove) throw new InvalidOperationException("Zuerst eine Figur ziehen.");
        if (value is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(value));
        EnsureActivePlayer();
        Die = value;
        Attempts++;
        AwaitingMove = true;
    }
    public int Target(int piece) => pieces[Player, piece] < 0 ? 0 : pieces[Player, piece] + Die;
    public bool IsCapture(int piece) => Target(piece) < TrackLength && EnemyAt(Square(Player, Target(piece)));
    private bool EnemyAt(int square) => Enumerable.Range(0, PlayerCount).Any(p => p != Player && Kinds[p] != PlayerKind.Inactive &&
        Enumerable.Range(0, 4).Any(i => pieces[p, i] >= 0 && pieces[p, i] < TrackLength && Square(p, pieces[p, i]) == square));
    private bool CanMove(int piece)
    {
        int from = pieces[Player, piece];
        if (from < 0 && Die != 6 && !(Die == 1 && Rules.EnterOnOne)) return false;
        int to = Target(piece);
        if (to >= TrackLength + 4) return false;
        for (int j = 0; j < 4; j++)
        {
            if (j == piece) continue;
            int other = pieces[Player, j];
            if (other == to) return false;
            if (other >= 0 && other > from && other < to && (Rules.NoJump || other >= TrackLength)) return false;
        }
        return !(Rules.Rest && to < TrackLength && IsRestSquare(Square(Player, to)) && EnemyAt(Square(Player, to)));
    }
    public int[] LegalMoves()
    {
        if (!AwaitingMove || Finished) return [];
        var all = Enumerable.Range(0, 4).Where(CanMove).ToArray();
        var captures = all.Where(IsCapture).ToArray();
        if (Rules.Capture && Rules.Absolute && captures.Length > 0) return captures;
        var enter = all.Where(i => pieces[Player, i] < 0).ToArray();
        // seg13:0dc7: if entering itself captures on the start field, capture
        // compulsion lets the player choose any capture ("eine beliebige aussuchen").
        if (enter.Length > 0) return Rules.Capture && captures.Intersect(enter).Any() ? captures : enter;
        if (Rules.Capture && Rules.Hard && captures.Length > 0) return captures;
        if (Enumerable.Range(0, 4).Any(i => pieces[Player, i] < 0))
        {
            int at = Enumerable.Range(0, 4).FirstOrDefault(i => pieces[Player, i] == 0, -1);
            if (at >= 0)
            {
                var visited = new HashSet<int>();
                while (visited.Add(at))
                {
                    if (all.Contains(at)) return [at];
                    int to = pieces[Player, at] + Die;
                    int blocker = Enumerable.Range(0, 4).FirstOrDefault(i => i != at && pieces[Player, i] == to, -1);
                    if (blocker < 0) return [];
                    at = blocker;
                }
                return [];
            }
        }
        if (Rules.Capture && captures.Length > 0) return captures;
        return all;
    }
    public bool Move(int piece)
    {
        if (!LegalMoves().Contains(piece)) throw new InvalidOperationException("Dieser Zug ist nicht erlaubt.");
        int to = Target(piece);
        bool captured = false;
        if (to < TrackLength)
            for (int p = 0; p < PlayerCount; p++)
                for (int i = 0; i < 4; i++)
                    if (p != Player && Kinds[p] != PlayerKind.Inactive && pieces[p, i] >= 0 && pieces[p, i] < TrackLength && Square(p, pieces[p, i]) == Square(Player, to))
                    { pieces[p, i] = -1; captured = true; }
        pieces[Player, piece] = to;
        if (Enumerable.Range(0, 4).All(i => pieces[Player, i] >= TrackLength))
        {
            Winners.Add(Player);
            var remaining=Enumerable.Range(0,PlayerCount).Where(p=>Kinds[p]!=PlayerKind.Inactive&&!Winners.Contains(p)).ToArray();
            // Original seg04:0a6c..0ad9 assigns the sole remaining player last place.
            if(remaining.Length==1)Winners.Add(remaining[0]);
        }
        EndTurn(false);
        return captured;
    }
    public void Pass()
    {
        if (!AwaitingMove || LegalMoves().Length != 0) throw new InvalidOperationException("Ein Zug ist möglich.");
        // seg04:06f1..079e: three entry attempts require every piece outside
        // the yard to occupy the last N goal slots, without gaps. A piece on
        // the track or in an unfinished goal arrangement gets only one roll.
        int outsideYard = Enumerable.Range(0, 4).Count(i => pieces[Player, i] >= 0);
        bool goalsPacked = Enumerable.Range(TrackLength + 4 - outsideYard, outsideYard)
            .All(goal => Enumerable.Range(0, 4).Any(i => pieces[Player, i] == goal));
        bool entryAttempts = goalsPacked && outsideYard < 4;
        // seg04:07f0..080c ends the third failed entry attempt before the
        // later RepeatOnOne check. EnterOnOne, not RepeatOnOne, exempts a one.
        bool exhausted = entryAttempts && Attempts >= 3 && Die != 6 && !(Die == 1 && Rules.EnterOnOne);
        EndTurn(entryAttempts && Attempts < 3, allowBonus: !exhausted);
    }
    public void EnsureActivePlayer()
    {
        if (Finished) { AwaitingMove = false; Attempts = 0; return; }
        for (int n = 0; n < PlayerCount && (Kinds[Player] == PlayerKind.Inactive || Winners.Contains(Player)); n++)
        { Player = (Player + 1) % PlayerCount; Attempts = 0; AwaitingMove=false; }
    }
    public void ReconcileTurn()
    {
        EnsureActivePlayer();
        if (AwaitingMove && LegalMoves().Length == 0) Pass();
    }
    // seg07:042c/0584: retain absolute track fields 1..40; reintroduce players 5/6 in their yards.
    public bool ChangeBoard(int players)
    {
        if (players is not (4 or 6)) throw new ArgumentOutOfRangeException(nameof(players));
        if (players == PlayerCount) return true;
        if (players == 4 && (Kinds.Take(4).All(k => k == PlayerKind.Inactive) ||
            Enumerable.Range(0,4).Any(p => Enumerable.Range(0,4).Any(i => pieces[p,i] >= 0 && pieces[p,i] < TrackLength && Square(p,pieces[p,i]) >= 40)))) return false;
        int oldLength = TrackLength, newLength = players == 4 ? 40 : 48;
        for (int p=0;p<4;p++)
            for (int i=0;i<4;i++)
            {
                int at=pieces[p,i];
                if (at >= oldLength) pieces[p,i]=newLength+at-oldLength;
                else if (at >= 0) pieces[p,i]=(Square(p,at)-p*(newLength/players)+newLength)%newLength;
            }
        for (int p=4;p<6;p++) for (int i=0;i<4;i++) pieces[p,i]=-1;
        Winners.RemoveAll(p=>p>=4);
        PlayerCount=players;
        if(Player>=players){Player=0;AwaitingMove=false;Attempts=0;}
        EnsureActivePlayer();
        return true;
    }
    /// <summary>Number of seg04:0022 next-player transitions; the window erases the die on each.</summary>
    public int Transitions { get; private set; }
    private void EndTurn(bool retry, bool allowBonus = true)
    {
        AwaitingMove = false;
        if (!(allowBonus && (Die == 6 || (Die == 1 && Rules.RepeatOnOne))) && !retry)
        {
            // seg04:0022: zuerst vorgemerkte Änderungen, dann DS:2176 erhöhen und
            // oberhalb der (eventuell neuen) Spielerzahl auf Spieler 1 umbrechen.
            int current = Player;
            Transitions++;
            ApplyPendingTurnChanges();
            Player = current + 1 >= PlayerCount ? 0 : current + 1; Attempts = 0;
        }
        else if (!retry) Attempts = 0;
        EnsureActivePlayer();
    }
    internal void SetPosition(int player, int piece, int position) => pieces[player, piece] = position;
    internal void RestoreTurn(int player, int die, bool awaitingMove, int attempts)
    { Player = player; Die = die; AwaitingMove = awaitingMove; Attempts = attempts; }
}
