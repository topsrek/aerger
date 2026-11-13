using Aerger;
using System.Drawing;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// research/emulate_ai.py: writes random in-game decisions (ERG bytes, the native choice as
// original field code, native and translated legal-move masks) for comparison with
// seg06:0640 and seg13:1140 executed from AERGER.EXE.
if (args.Length == 3 && args[0] == "--dump-ai-cases")
{
    int wanted = int.Parse(args[2]), written = 0;
    using var output = File.Create(args[1]);
    for (int seed = 0; written < wanted; seed++)
    {
        var random = new Random(seed);
        int players = seed % 2 == 0 ? 4 : 6;
        var state = new Game(players, random.Next(players)) { Rules = new Rules(random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0) };
        for (int p = 0; p < 6; p++) state.Kinds[p] = p < players && random.Next(5) == 0 ? PlayerKind.Inactive : PlayerKind.Computer;
        if (Enumerable.Range(0, players).All(p => state.Kinds[p] == PlayerKind.Inactive)) state.Kinds[0] = PlayerKind.Computer;
        state.EnsureActivePlayer();
        for (int turn = 0; turn < 3000 && !state.Finished && written < wanted; turn++)
        {
            state.Roll(random.Next(1, 7));
            int[] moves = state.LegalMoves();
            if (moves.Length == 0) { state.Pass(); continue; }
            int choice = ComputerStrategy.Choose(state);
            byte[] save = OriginalSave.Write(state);
            // Mask bytes: native legal moves, then the literal seg13:1140 translation incl. the 0e32 boundary.
            int Mask(int[] pieces) => pieces.Aggregate(0, (mask, piece) => mask | 1 << piece);
            output.Write(save); output.WriteByte(save[147 + state.Player * 4 + choice]);
            output.WriteByte((byte)Mask(moves)); output.WriteByte((byte)Mask(OriginalMoveCheck.LegalMoves(state))); written++;
            state.Move(random.Next(3) == 0 ? moves[random.Next(moves.Length)] : choice);
        }
    }
    Console.WriteLine($"{written} KI-Entscheidungen geschrieben.");
    return;
}
// research/emulate_turns.py: writes roll and move transitions of random games as
// kind (0 roll, 1 move), ERG before, argument (die or field code), native ERG after,
// for comparison with seg04:065c and seg04:0ca3 + seg05:0022 executed from AERGER.EXE.
if (args.Length == 3 && args[0] == "--dump-turn-cases")
{
    int wanted = int.Parse(args[2]), written = 0;
    using var output = File.Create(args[1]);
    void Record(byte kind, byte[] before, byte argument, Game after)
    { output.WriteByte(kind); output.Write(before); output.WriteByte(argument); output.Write(OriginalSave.Write(after)); written++; }
    for (int seed = 0; written < wanted; seed++)
    {
        var random = new Random(seed);
        int players = seed % 2 == 0 ? 4 : 6;
        var state = new Game(players, random.Next(players)) { Rules = new Rules(random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0) };
        for (int p = 0; p < 6; p++) state.Kinds[p] = p < players && random.Next(5) == 0 ? PlayerKind.Inactive : PlayerKind.Computer;
        if (Enumerable.Range(0, players).All(p => state.Kinds[p] == PlayerKind.Inactive)) state.Kinds[0] = PlayerKind.Computer;
        state.EnsureActivePlayer();
        for (int turn = 0; turn < 3000 && !state.Finished && written < wanted; turn++)
        {
            int die = random.Next(1, 7);
            byte[] before = OriginalSave.Write(state);
            before[328] = (byte)die;
            state.Roll(die);
            int[] moves = state.LegalMoves();
            if (moves.Length == 0) state.Pass();
            Record(0, before, (byte)die, state);
            if (moves.Length == 0) continue;
            int piece = random.Next(3) == 0 ? moves[random.Next(moves.Length)] : ComputerStrategy.Choose(state);
            before = OriginalSave.Write(state);
            state.Move(piece);
            Record(1, before, before[147 + before[330] * 4 - 4 + piece], state);
        }
    }
    Console.WriteLine($"{written} Wurf-/Zugübergänge geschrieben.");
    return;
}
// research/emulate_turns.py --pending: like --dump-turn-cases, with changes from the player,
// rules and board dialogs pending (seg08) before each case. Each pending block is a flags byte
// (1 players, 2 rules, 4 to six, 8 to four), the 138-byte pending player records (DS:1cd6) and
// the 7 pending rule bytes (DS:1d6e); one block before and one after the native transition.
if (args.Length == 3 && args[0] == "--dump-pending-cases")
{
    int wanted = int.Parse(args[2]), written = 0;
    using var output = File.Create(args[1]);
    byte[] Pending(Game g)
    {
        var block = new byte[146];
        block[0] = (byte)((g.PendingKinds is null ? 0 : 1) | (g.PendingRules is null ? 0 : 2) | (g.PendingBoard == 6 ? 4 : 0) | (g.PendingBoard == 4 ? 8 : 0));
        if (g.PendingKinds is not null)
        {
            var records = new Game(g.PlayerCount);
            for (int p = 0; p < 6; p++) { records.Names[p] = g.PendingNames![p]; records.Kinds[p] = g.PendingKinds[p]; }
            OriginalSave.Write(records).AsSpan(0, 138).CopyTo(block.AsSpan(1));
        }
        if (g.PendingRules is not null)
            OriginalSave.Write(new Game { Rules = g.PendingRules }).AsSpan(140, 7).CopyTo(block.AsSpan(139));
        return block;
    }
    Rules RandomRules(Random random) => new(random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0);
    for (int seed = 0; written < wanted; seed++)
    {
        var random = new Random(seed);
        int players = seed % 2 == 0 ? 4 : 6;
        var state = new Game(players, random.Next(players)) { Rules = RandomRules(random) };
        for (int p = 0; p < 6; p++) state.Kinds[p] = p < players && random.Next(5) == 0 ? PlayerKind.Inactive : PlayerKind.Computer;
        if (Enumerable.Range(0, players).All(p => state.Kinds[p] == PlayerKind.Inactive)) state.Kinds[0] = PlayerKind.Computer;
        state.EnsureActivePlayer();
        bool over = false;
        for (int turn = 0; turn < 3000 && !over && written < wanted; turn++)
        {
            if (!state.HasPendingChanges && random.Next(8) == 0)
            {
                int what = random.Next(1, 16);
                if ((what & 1) != 0)
                {
                    var names = Enumerable.Range(0, 6).Select(p => random.Next(3) == 0 ? $"Neu {seed % 100} {p + 1}" : state.Names[p]).ToArray();
                    var kinds = Enumerable.Range(0, 6).Select(p => random.Next(3) == 0 ? (PlayerKind)random.Next(3) : state.Kinds[p]).ToArray();
                    // seg08:04c8..051d: without an active player on the current board player 1 becomes human.
                    if (Enumerable.Range(0, state.PlayerCount).All(p => kinds[p] == PlayerKind.Inactive)) kinds[0] = PlayerKind.Human;
                    state.SetPendingPlayers(names, kinds);
                }
                if ((what & 2) != 0) state.PendingRules = RandomRules(random);
                if ((what & 4) != 0) state.PendingBoard = state.PlayerCount == 4 ? 6 : 4;
            }
            int die = random.Next(1, 7);
            byte[] before = OriginalSave.Write(state), pendingBefore = Pending(state);
            before[328] = (byte)die;
            state.Roll(die);
            int[] moves = state.LegalMoves();
            if (moves.Length == 0) state.Pass();
            void Record(byte kind, byte argument)
            {
                // seg04:15ae applies everything pending when the game ends (GameWindow calls this).
                if (state.Finished) { state.ApplyPendingAfterGame(); over = true; }
                output.WriteByte(kind); output.Write(before); output.WriteByte(argument); output.Write(pendingBefore);
                output.Write(OriginalSave.Write(state)); output.Write(Pending(state)); written++;
            }
            Record(0, (byte)die);
            state.TakeBoardChangeRejected();
            if (moves.Length == 0 || over) continue;
            int piece = random.Next(3) == 0 ? moves[random.Next(moves.Length)] : ComputerStrategy.Choose(state);
            before = OriginalSave.Write(state); pendingBefore = Pending(state);
            byte field = before[147 + before[330] * 4 - 4 + piece];
            state.Move(piece);
            Record(1, field);
            state.TakeBoardChangeRejected();
        }
    }
    Console.WriteLine($"{written} Übergänge mit vorgemerkten Änderungen geschrieben.");
    return;
}
var originalBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "BEISPIEL.ERG"));
var example = OriginalSave.Read(originalBytes);
Check(example.PlayerCount == 4 && example.Player == 1 && example.Die == 2 && !example.AwaitingMove, "Original-Beispiel: Zugzustand.");
Check(example.Position(0, 3) == 41 && example.Position(1, 0) == 43 && example.Position(2, 2) == 3, "Original-Beispiel: Feldzuordnung.");
Check(example.Kinds[0] == PlayerKind.Human && example.Kinds[4] == PlayerKind.Computer, "Original-Beispiel: Spielertypen (Spielerdialog seg08:039c).");
var exported = OriginalSave.Write(example);
Check(exported.AsSpan(138).SequenceEqual(originalBytes.AsSpan(138)), "Original-Beispiel: alle 193 Zustandsbytes müssen beim Export identisch bleiben.");
for (int p = 0; p < 6; p++)
    Check(exported.AsSpan(p*23, originalBytes[p*23]+1).SequenceEqual(originalBytes.AsSpan(p*23,originalBytes[p*23]+1)), "Original-Namen.");
foreach (var invalid in new[] { originalBytes[..330], new byte[331] })
{
    bool rejected = false;
    try { OriginalSave.Read(invalid); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Beschädigte ERG-Datei zurückweisen.");
}
{
    // seg07:042c (six to four) leaves the pieces of players 5/6 where they were, also on fields
    // 41..48; seg07:0584 puts inactive players 5/6 in their yards. Such saves must load.
    var stale = (byte[])originalBytes.Clone();
    stale[4 * 23 + 22] = (byte)PlayerKind.Computer; stale[147 + 16] = 45; stale[147 + 17] = 12;
    stale[5 * 23 + 22] = (byte)PlayerKind.Inactive; stale[147 + 20] = 101;
    var loaded = OriginalSave.Read(stale);
    Check(loaded.PlayerCount == 4 && loaded.Position(4, 0) == -1 && loaded.Position(5, 0) == -1, "Altdaten der Spieler 5/6 auf dem Viererbrett.");
    Check(loaded.ChangeBoard(6) && Enumerable.Range(4, 2).All(p => Enumerable.Range(0, 4).All(i => loaded.Position(p, i) == -1)), "Spieler 5/6 starten nach dem Wechsel im Haus.");
}
Console.WriteLine("OK: Original-BEISPIEL.ERG gelesen und Zustandsdaten bytegenau zurückgeschrieben.");
{
    var originalCfg = Path.Combine(AppContext.BaseDirectory, "AERGER.CFG");
    var cfg = File.ReadAllBytes(originalCfg);
    Check(SettingsFile.TryRead(originalCfg)?.SequenceEqual(cfg) == true, "Original-AERGER.CFG muss gültig sein.");
    byte[] Changed(int index, byte value) { var copy = (byte[])cfg.Clone(); copy[index] = value; return copy; }
    foreach (var invalid in new[] { [], cfg[..12], [.. cfg, 0], Changed(0, 2), Changed(8, 255), Changed(9, 4), Changed(10, 4),
                                    Changed(11, 0), Changed(11, 4), Changed(12, 2) })
        Check(!SettingsFile.IsValid(invalid), $"Ungültige AERGER.CFG ({Convert.ToHexString(invalid)}) muss abgelehnt werden.");
    var scratch = Path.Combine(Path.GetTempPath(), $"aerger-cfg-{Guid.NewGuid():N}");
    Directory.CreateDirectory(scratch);
    try
    {
        var path = Path.Combine(scratch, "AERGER.CFG");
        Check(SettingsFile.TryRead(path) is null, "Fehlende AERGER.CFG: Standardwerte.");
        foreach (var damaged in new[] { [], cfg[..7], Changed(11, 9), new byte[4096] })
        {
            File.WriteAllBytes(path, damaged);
            Check(SettingsFile.TryRead(path) is null, "Beschädigte AERGER.CFG darf den Start nicht verhindern.");
        }
        File.WriteAllBytes(path, cfg);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(SettingsFile.TryRead(path) is null, "Gesperrte AERGER.CFG darf den Start nicht verhindern.");
        Check(SettingsFile.TryRead(path)?.SequenceEqual(cfg) == true, "Wieder freigegebene AERGER.CFG wird gelesen.");
    }
    finally { Directory.Delete(scratch, true); }
    Console.WriteLine("OK: AERGER.CFG gültig gelesen; fehlende, beschädigte und gesperrte Dateien führen zu Standardwerten.");
}
var switched = OriginalSave.Read(originalBytes);
Check(switched.ChangeBoard(6), "Wechsel auf sechs Spieler.");
Check(switched.Position(0,3)==49 && switched.Square(1,switched.Position(1,1))==29, "Brettwechsel erhält Ziel und absolute Bahnfelder.");
Check(switched.ChangeBoard(4), "Rückwechsel auf vier Spieler.");
Check(OriginalSave.Write(switched).AsSpan(138).SequenceEqual(exported.AsSpan(138)), "Brettwechsel erhält Beispielstellung.");
var blockedBoard = new Game(6);
blockedBoard.SetPosition(0,0,40);
Check(!blockedBoard.ChangeBoard(4) && blockedBoard.PlayerCount==6 && blockedBoard.Position(0,0)==40, "Besetzte Zusatzfelder verhindern Brettwechsel ohne Datenverlust.");
var savedTurn = new Game(6) { Rules = new Rules(EnterOnOne:true) };
savedTurn.Names[0]="Ärger Ö ß";
savedTurn.Roll(1);
var reloadedTurn=OriginalSave.Read(OriginalSave.Write(savedTurn));
Check(reloadedTurn.AwaitingMove && reloadedTurn.Attempts==1 && reloadedTurn.LegalMoves().SequenceEqual(savedTurn.LegalMoves()) && reloadedTurn.Names[0]==savedTurn.Names[0], "Wurf und Windows-1252-Namen erhalten.");

var game = new Game();
for (int attempt = 0; attempt < 3; attempt++)
{
    game.Roll(2);
    Check(game.LegalMoves().Length == 0, "Ohne Sechs darf keine Figur starten.");
    game.Pass();
    Check(game.Player == (attempt == 2 ? 1 : 0), "Drei Startversuche.");
}
game.Roll(6);
Check(game.LegalMoves().Length == 4, "Alle vier Figuren können starten.");
game.Move(0);
Check(game.Player == 1 && game.Position(1, 0) == 0, "Sechs erlaubt weiteren Wurf.");
game.Roll(6);
Check(game.LegalMoves().SequenceEqual(new[] { 0 }), "Eigene Figur blockiert Startfeld.");
game.Move(0);

// Deterministic full games exercise captures, home entry, turn changes and winning.
int captures = 0;
for (int seed = 0; seed < 30; seed++)
{
    game = new Game();
    var random = new Random(seed);
    for (int turn = 0; turn < 20000 && game.Winner < 0; turn++)
    {
        game.Roll(random.Next(1, 7));
        int[] moves = game.LegalMoves();
        if (moves.Length == 0) game.Pass();
        else if (game.Move(moves[random.Next(moves.Length)])) captures++;
        var occupied = new HashSet<int>();
        for (int p = 0; p < 4; p++)
        {
            var home = new HashSet<int>();
            for (int i = 0; i < 4; i++)
            {
                int position = game.Position(p, i);
                Check(position is >= -1 and <= 43, "Figur außerhalb des Bretts.");
                if (position is >= 0 and < 40) Check(occupied.Add(game.Square(p, position)), "Doppelt belegtes Feld.");
                if (position >= 40) Check(home.Add(position), "Doppelt belegtes Zielfeld.");
            }
        }
    }
    Check(game.Winner >= 0, "Spiel muss enden.");
    Check(Enumerable.Range(0, 4).All(i => game.Position(game.Winner, i) >= 40), "Sieger hat alle Figuren im Ziel.");
}
Check(captures > 0, "Simulation muss Schlagen abdecken.");
Console.WriteLine($"OK: Startregeln und 30 vollständige Spiele, {captures} Schlagzüge.");
// Values of seg25:0fbd executed from AERGER.EXE (research/ne_emulator.py).
{
    var zero = new OriginalRandom(0);
    Check(Enumerable.Range(0, 12).Select(_ => zero.Next(6)).SequenceEqual(new[] { 0, 0, 5, 1, 1, 4, 1, 0, 2, 2, 0, 2 }), "Original-Zufall: Folge ab RandSeed 0.");
    var other = new OriginalRandom(0x12345678);
    Check(Enumerable.Range(0, 12).Select(_ => other.Next(6)).SequenceEqual(new[] { 4, 2, 5, 5, 5, 5, 3, 4, 2, 5, 4, 0 }), "Original-Zufall: Folge ab RandSeed 0x12345678.");
    Check(OriginalRandom.SeedFromClock(new DateTime(2026, 11, 3, 13, 45, 7, 890)) == (7u << 24 | 89u << 16 | 13u << 8 | 45u), "Original-Zufall: Randomize aus der DOS-Uhrzeit.");
    Console.WriteLine("OK: Würfelgenerator identisch mit seg25:0fbd/101a/1052.");
}

// seg04:06f1..079e grants three entry attempts only with packed rear goal
// slots. Merely having no piece on the track is insufficient.
foreach (int players in new[] {4,6})
{
    foreach (int goals in new[] {0,1,2,3})
    {
        var packed = new Game(players);
        for(int i=0;i<goals;i++)packed.SetPosition(0,i,packed.TrackLength+3-i);
        for(int attempt=1;attempt<=3;attempt++)
        {
            packed.Roll(5);
            Check(packed.LegalMoves().Length==0,"Gepacktes Ziel: kein Zug mit fünf.");
            packed.Pass();
            Check(packed.Player==(attempt==3?1:0)&&packed.Attempts==(attempt==3?0:attempt),
                $"{players} Spieler, {goals} Zielfiguren: drei Versuche nur bei lückenlos belegtem Zielende.");
            packed=OriginalSave.Read(OriginalSave.Write(packed));
        }
    }
    foreach(int[] slots in new[] {new[]{0},new[]{1},new[]{2},new[]{0,3},new[]{1,3},new[]{0,2,3}})
    {
        var gap = new Game(players);
        for(int i=0;i<slots.Length;i++)gap.SetPosition(0,i,gap.TrackLength+slots[i]);
        gap.Roll(5);Check(gap.LegalMoves().Length==0,"Ziellücke: fünf ist zu hoch.");
        gap.Pass();
        Check(gap.Player==1&&!gap.AwaitingMove&&gap.Attempts==0,
            $"{players} Spieler, Zielfelder [{string.Join(',',slots)}]: hoher Wurf gewährt keine Startwiederholung.");
    }
    var loadedGap = new Game(players);
    loadedGap.SetPosition(0,0,loadedGap.TrackLength);loadedGap.Roll(5);
    loadedGap.Kinds[1]=PlayerKind.Inactive;loadedGap.Winners.Add(2);
    loadedGap=OriginalSave.Read(OriginalSave.Write(loadedGap));loadedGap.ReconcileTurn();
    Check(loadedGap.Player==3&&!loadedGap.AwaitingMove&&loadedGap.Attempts==0,
        "Geladener hoher Wurf bei Ziellücke überspringt inaktive und platzierte Spieler.");
    var canEnter = new Game(players) {Rules=new Rules(EnterOnOne:true)};
    canEnter.SetPosition(0,0,canEnter.TrackLength);
    canEnter.Roll(1);canEnter.Move(1);
    Check(canEnter.Player==1,"Raussetzen mit eins gibt ohne Zusatzregel keinen weiteren Wurf.");
    var repeatAttempt = new Game(players) {Rules=new Rules(RepeatOnOne:true)};
    repeatAttempt.SetPosition(0,0,repeatAttempt.TrackLength+3);repeatAttempt.SetPosition(0,1,repeatAttempt.TrackLength+2);
    repeatAttempt.SetPosition(0,2,repeatAttempt.TrackLength+1);
    repeatAttempt.Roll(1);Check(repeatAttempt.LegalMoves().Length==0,"Gepacktes Ziel, eins ohne Raussetzen.");
    repeatAttempt.Pass();Check(repeatAttempt.Player==0,"Erneutes Würfeln bei eins bleibt unabhängig vom Startversuch erhalten.");
    repeatAttempt.Roll(1);repeatAttempt.Pass();
    repeatAttempt.Roll(1);repeatAttempt.Pass();
    Check(repeatAttempt.Player==1&&repeatAttempt.Attempts==0,
        "Dritter erfolgloser Startversuch beendet den Zug auch bei Nochmal bei eins ohne Raussetzen mit eins.");
    var ordinaryRepeat = new Game(players) {Rules=new Rules(RepeatOnOne:true)};
    ordinaryRepeat.SetPosition(0,0,ordinaryRepeat.TrackLength+2);
    ordinaryRepeat.SetPosition(0,1,ordinaryRepeat.TrackLength+3);
    ordinaryRepeat.SetPosition(0,2,0);ordinaryRepeat.SetPosition(0,3,1);
    ordinaryRepeat.Rules=ordinaryRepeat.Rules with {NoJump=true};
    ordinaryRepeat.Roll(1);ordinaryRepeat.Move(3);
    Check(ordinaryRepeat.Player==0&&ordinaryRepeat.Attempts==0,"Nochmal bei eins bleibt nach einem normalen Zug erhalten.");
}
Console.WriteLine("OK: Drei Startversuche nur bei lückenlos belegten hintersten Zielfeldern auf beiden Brettern.");

// Regression cases derived from ERGWIN.HLP rather than from the implementation.
static Game Position(int[] own, int die, Rules? rules = null, params (int Player, int Piece, int Distance)[] opponents)
{
    var state = new Game { Rules = rules ?? new Rules() };
    for (int i = 0; i < own.Length; i++) state.SetPosition(0, i, own[i]);
    foreach (var p in opponents) state.SetPosition(p.Player, p.Piece, p.Distance);
    state.Roll(die);
    return state;
}
static void Moves(Game state, params int[] expected) => Check(state.LegalMoves().SequenceEqual(expected),
    $"Erwartete Züge [{string.Join(',', expected)}], tatsächlich [{string.Join(',', state.LegalMoves())}].");

// Entry and recursively clearing start take precedence over arbitrary movement.
Moves(Position([5, -1, -1, -1], 6), 1, 2, 3);
Moves(Position([0, 6, 12, -1], 6), 2);
Moves(Position([0, 10, -1, -1], 3), 0);

// The same position distinguishes all three capture-priority variants.
var enemy = (Player: 1, Piece: 0, Distance: 3); // absolute square 13
Moves(Position([0, 10, -1, -1], 3, new Rules(Capture: true), enemy), 0);
Moves(Position([0, 10, -1, -1], 3, new Rules(Capture: true, Hard: true), enemy), 1);
Moves(Position([0, 10, -1, -1], 3, new Rules(Capture: true, Absolute: true), enemy), 1);
Moves(Position([7, -1, -1, -1], 6, new Rules(Capture: true, Hard: true), enemy), 1, 2, 3);
Moves(Position([7, -1, -1, -1], 6, new Rules(Capture: true, Absolute: true), enemy), 0);

// Protected opponents are an impassable destination, but may be jumped over.
Moves(Position([0, 10, -1, -1], 4, new Rules(Rest: true), (1, 0, 34)));
Moves(Position([0, 10, -1, -1], 5, new Rules(Rest: true), (1, 0, 34)), 0);
Moves(Position([5, 7, 20, 30], 3, new Rules(NoJump: true)), 1, 2, 3);
Moves(Position([38, 40, 42, 43], 3));
Moves(Position([39, 41, 42, 43], 1), 0);
Moves(Position([-1, -1, -1, -1], 1, new Rules(EnterOnOne: true)), 0, 1, 2, 3);
var repeat = Position([5, 10, 20, 30], 1, new Rules(RepeatOnOne: true));
repeat.Move(0);
Check(repeat.Player == 0, "Einser-Wiederholungsregel.");
var trials = Position([-1, 41, 42, 43], 5);
trials.Pass();
Check(trials.Player == 0, "Drei Versuche auch bei Figuren im Ziel.");
var finish = Position([39, 41, 42, 43], 1);
finish.Move(0);
Check(finish.Winners.SequenceEqual(new[] { 0 }) && finish.Player == 1 && !finish.Finished, "Nach einem Sieger weiterspielen.");
var twoPlayers=Position([39,41,42,43],1);
twoPlayers.Kinds[2]=twoPlayers.Kinds[3]=PlayerKind.Inactive;
twoPlayers.Move(0);
Check(twoPlayers.Finished && twoPlayers.Winners.SequenceEqual(new[]{0,1}) && twoPlayers.Position(1,0)==-1,
    "Original seg04:0a6c: letzter Spieler erhält seinen Platz ohne allein weiterzuspielen.");
var strategyCase=Position([40,10,20,30],1,null,(1,0,1));
Check(ComputerStrategy.Score(strategyCase,0)==-3 && ComputerStrategy.Choose(strategyCase)==0,
    "Originalstrategie: Zielzug (-3) hat Vorrang vor einfachem Schlagen.");
var tieCase=Position([5,12,22,27],1);
Check(ComputerStrategy.Choose(tieCase)==0,"Originalstrategie: erster Zug bei Gleichstand.");

// Original rest-field sets DS:1790/DS:17b0 (research/dump_ai_tables.py): absolute
// fields 5,15,25,35 on the four board, but 4,12,20,28,36,44 on the six board.
Check(Enumerable.Range(0,40).Where(new Game(4).IsRestSquare).SequenceEqual(new[]{4,14,24,34}),"Ruhefelder Viererbrett (DS:1790).");
Check(Enumerable.Range(0,48).Where(new Game(6).IsRestSquare).SequenceEqual(new[]{3,11,19,27,35,43}),"Ruhefelder Sechserbrett (DS:17b0).");
static Game SixRest(int die,int enemyDistance)
{
    var state=new Game(6){Rules=new Rules(Rest:true)};
    state.SetPosition(0,0,0);
    if(enemyDistance>=0)state.SetPosition(1,0,enemyDistance); // player 2 starts on square 8
    state.Roll(die);
    return state;
}
Moves(SixRest(3,43));        // absolute field 4 protects the opponent
Moves(SixRest(4,44),0);      // field 5 is an ordinary field on the six board
Check(ComputerStrategy.Score(SixRest(3,-1),0)==-1 && ComputerStrategy.Score(SixRest(4,-1),0)==0,
    "Originalstrategie seg06:0260: Ruhefeld-Bonus auf dem Sechserbrett bei Feld 4.");
var fourRest=Position([0,-1,-1,-1],4,new Rules(Rest:true));
var fourPlain=Position([0,-1,-1,-1],3,new Rules(Rest:true));
Check(ComputerStrategy.Score(fourRest,0)==-1 && ComputerStrategy.Score(fourPlain,0)==0,
    "Originalstrategie seg06:0260: Ruhefeld-Bonus auf dem Viererbrett bei Feld 5.");
var leaveRest=new Game(6){Rules=new Rules(Rest:true)};
leaveRest.SetPosition(0,0,3);leaveRest.Roll(2);
Check(ComputerStrategy.Score(leaveRest,0)==1,"Originalstrategie seg06:0363: Verlassen des Ruhefelds kostet einen Punkt.");
var smallPanel=PlayerPanelLayout.ForBoard(200);
var mediumPanel=PlayerPanelLayout.ForBoard(400);
var largePanel=PlayerPanelLayout.ForBoard(700);
Check(smallPanel.Bounds==new Rectangle(205,2,46,50) && smallPanel.Die==new Point(220,80) && smallPanel.FontPixels==8,
    "Kleines Spielerfeld entspricht den Originalkoordinaten.");
Check(mediumPanel.Bounds==new Rectangle(405,2,92,100) && mediumPanel.Die==new Point(439,160) && mediumPanel.FontPixels==12,
    "Mittleres Spielerfeld entspricht den Originalkoordinaten.");
Check(largePanel.Bounds==new Rectangle(705,2,151,150) && largePanel.Die==new Point(754,300) && largePanel.FontPixels==24,
    "Großes Spielerfeld entspricht den Originalkoordinaten.");
foreach(int boardSize in new[]{200,400,700})
{
    var panel=PlayerPanelLayout.ForBoard(boardSize);
    Check(panel.Bounds.Contains(panel.FourPlayerFigure) && panel.Bounds.Contains(panel.SixPlayerFigure) && panel.Bounds.Contains(panel.Name),
        "Spielerfigur und Name liegen im Spielerfeld.");
    Check(panel.Die.Y>panel.Bounds.Bottom,"Würfel liegt separat unterhalb des Spielerfelds.");
    int diePixels=boardSize==200?14:boardSize==400?28:54;
    Check(panel.Copyright.Y>panel.Die.Y+diePixels && panel.Copyright.Y+4*panel.CopyrightLineHeight<=boardSize,
        "Copyright-Text überdeckt den Würfel nicht und bleibt vollständig im Fenster.");
}
Console.WriteLine("OK: Originalmaße des Spielerfelds und separate Würfelposition für alle drei Größen.");
Check(WindowSizing.ClientForBoard(200)==new Size(255,204) &&
    WindowSizing.ClientForBoard(400)==new Size(501,404) &&
    WindowSizing.ClientForBoard(700)==new Size(860,704),"Originale Fenster-Innenmaße.");
foreach(int boardSize in new[]{200,400,700})
{
    var client=WindowSizing.ClientForBoard(boardSize);
    Check(WindowSizing.LargestFittingBoard(client)==boardSize,"Exakte Größengrenze bleibt erhalten.");
    if(boardSize>200)
    {
        int smaller=boardSize==700?400:200;
        Check(WindowSizing.LargestFittingBoard(new(client.Width-1,client.Height))==smaller &&
            WindowSizing.LargestFittingBoard(new(client.Width,client.Height-1))==smaller,
            "Beide Fensterdimensionen müssen für die größere Stufe ausreichen.");
    }
}
Check(WindowSizing.LargestFittingBoard(new(850,1000))==400 &&
    WindowSizing.LargestFittingBoard(new(1200,600))==400 &&
    WindowSizing.LargestFittingBoard(new(1,1))==200 &&
    WindowSizing.LargestFittingBoard(new(1920,1080))==700,"Größenwahl bei schmalen, flachen und großen Fenstern.");
Console.WriteLine("OK: Fenstergrößen und Stufenwahl an Breiten-/Höhengrenzen.");
var changedRules=Position([5,7,20,30],3);
changedRules.Rules=new Rules(NoJump:true);
changedRules.ReconcileTurn();
Check(changedRules.Player==0 && changedRules.AwaitingMove && changedRules.Die==3 && changedRules.Attempts==1,
    "Regelwechsel erhält einen weiterhin spielbaren Wurf.");
Moves(changedRules,1,2,3);
var lostEntry=Position([-1,-1,-1,-1],1,new Rules(EnterOnOne:true));
lostEntry.Rules=new Rules();
lostEntry.ReconcileTurn();
Check(!lostEntry.AwaitingMove && lostEntry.Player==0 && lostEntry.Attempts==1,
    "Entfallener Einser-Eintritt beendet die Zugauswahl, erhält aber den nächsten Startversuch.");
var settledBytes=OriginalSave.Write(lostEntry);
lostEntry.ReconcileTurn();
Check(OriginalSave.Write(lostEntry).SequenceEqual(settledBytes),"Wiederholter Zugabgleich verbraucht keinen weiteren Versuch.");
lostEntry.Roll(2);lostEntry.ReconcileTurn();
lostEntry.Roll(2);lostEntry.ReconcileTurn();
Check(lostEntry.Player==1 && !lostEntry.AwaitingMove && lostEntry.Attempts==0,
    "Nach dem dritten erfolglosen Startversuch wechselt der Spieler auch nach einem Regelwechsel.");
var changedKind=Position([5,10,20,30],2);
changedKind.SetPlayerKind(0,PlayerKind.Computer);
changedKind.ReconcileTurn();
Check(changedKind.Player==0 && changedKind.AwaitingMove && changedKind.Die==2 && changedKind.Attempts==1,
    "Wechsel Mensch/Computer erhält den ausstehenden Zug.");
changedKind.SetPlayerKind(0,PlayerKind.Inactive);
changedKind.ReconcileTurn();
Check(changedKind.Player==1 && !changedKind.AwaitingMove && changedKind.Attempts==0,
    "Deaktivierter aktueller Spieler übergibt ohne den alten Wurf weiterzugeben.");
var inactiveSave=Position([5,10,20,30],2);
inactiveSave.Kinds[0]=PlayerKind.Inactive;
var inactiveReload=OriginalSave.Read(OriginalSave.Write(inactiveSave));
inactiveReload.ReconcileTurn();
Check(inactiveReload.Player==1 && !inactiveReload.AwaitingMove && inactiveReload.Attempts==0,
    "Geladener Spielstand mit inaktivem aktuellem Spieler bleibt bedienbar.");
var noPlayers=Position([5,10,20,30],2);
Array.Fill(noPlayers.Kinds,PlayerKind.Inactive);
noPlayers.ReconcileTurn();
Check(noPlayers.Finished && !noPlayers.AwaitingMove && noPlayers.Attempts==0,
    "Alle Spieler inaktiv: kein veralteter Zug bleibt ausstehend.");
noPlayers.Kinds[2]=PlayerKind.Human;
noPlayers.ReconcileTurn();
Check(!noPlayers.Finished && noPlayers.Player==2 && !noPlayers.AwaitingMove,
    "Reaktivierter Spieler beginnt mit einem neuen Wurf.");
noPlayers.Roll(6);noPlayers.Move(0);
Check(noPlayers.Position(2,0)==0,"Nach Reaktivierung sind Würfeln und Ziehen möglich.");
var rankedTurn=Position([40,41,42,43],1);
rankedTurn.Winners.Add(0);
rankedTurn.ReconcileTurn();
Check(rankedTurn.Player==1 && !rankedTurn.AwaitingMove && rankedTurn.Attempts==0,
    "Bereits platzierter aktueller Spieler wird ohne Weitergabe seines Wurfs übersprungen.");
var removedPlayer=new Game(6,5);
removedPlayer.Roll(6);
Check(removedPlayer.ChangeBoard(4),"Brettwechsel darf den aktuellen fünften/sechsten Spieler entfernen.");
removedPlayer.ReconcileTurn();
Check(removedPlayer.Player==0 && !removedPlayer.AwaitingMove && removedPlayer.Attempts==0,
    "Entfernter Spieler hinterlässt beim Brettwechsel keinen ausstehenden Zug.");
foreach(int players in new[]{4,6})
{
    var lifecycle=new Game(players);
    int target=lifecycle.TrackLength/players+5;
    lifecycle.SetPosition(0,0,target-2);
    lifecycle.SetPosition(1,0,5);
    lifecycle.SetPosition(1,1,lifecycle.TrackLength+1);
    lifecycle.SetPosition(1,2,lifecycle.TrackLength+3);
    lifecycle.Roll(2);
    byte[] beforeKind=OriginalSave.Write(lifecycle);
    lifecycle.SetPlayerKind(1,PlayerKind.Computer);
    lifecycle.SetPlayerKind(1,PlayerKind.Human);
    Check(OriginalSave.Write(lifecycle).SequenceEqual(beforeKind),"Mensch/Computer-Wechsel erhält Bahn, Ziel und ausstehenden Wurf.");
    lifecycle.SetPlayerKind(1,PlayerKind.Inactive);
    Check(Enumerable.Range(0,4).All(i=>lifecycle.Position(1,i)==-1),"Deaktivierung entfernt alle Bahn- und Zielfiguren.");
    byte[] disabled=OriginalSave.Write(lifecycle);
    Check(disabled.AsSpan(151,4).SequenceEqual(new byte[4])&&disabled.AsSpan(271,4).SequenceEqual(new byte[4])&&
        disabled.AsSpan(295,4).SequenceEqual(new byte[4])&&disabled[171+target*2]==0,
        "Originaldarstellung nach Deaktivierung: Felder, Hof-/Zielflags und Bahnbelegung werden null.");
    var restored=OriginalSave.Read(disabled);
    Check(OriginalSave.Write(restored).SequenceEqual(disabled),"ERG mit entfernten Figuren (Feld 0) bytegenau zurückschreiben.");
    Check(!lifecycle.Move(0)&&lifecycle.Position(0,0)==target,"Ehemaliges Feld eines deaktivierten Spielers ist ohne Schlag frei.");
    lifecycle.SetPlayerKind(1,PlayerKind.Human);
    Check(Enumerable.Range(0,4).All(i=>lifecycle.Position(1,i)==-1)&&lifecycle.Position(0,0)==target,
        "Reaktivierung setzt Figuren in den Hof, ohne eine fremde Figur zu überlagern.");
    Check(lifecycle.Player==2&&!lifecycle.AwaitingMove,"Reaktivierter Spieler übernimmt keinen alten Wurf.");
    byte[] reactivated=OriginalSave.Write(lifecycle);
    Check(reactivated.AsSpan(151,4).SequenceEqual(new byte[]{101,102,103,104})&&
        reactivated.AsSpan(271,4).SequenceEqual(new byte[]{1,1,1,1}),"Reaktivierung erzeugt die vier Original-Hofplätze.");
    Check(OriginalSave.Write(OriginalSave.Read(reactivated)).SequenceEqual(reactivated),"Reaktivierte Partie bleibt ohne doppelte Felder ladbar.");
    // Independently construct the original zero-field encoding, rather than
    // relying only on the native writer to supply the reader's test fixture.
    byte[] originalDisabled=OriginalSave.Write(new Game(players));
    originalDisabled[45]=2;
    Array.Clear(originalDisabled,151,4);Array.Clear(originalDisabled,271,4);
    var zeroFields=OriginalSave.Read(originalDisabled);
    Check(zeroFields.Kinds[1]==PlayerKind.Inactive&&OriginalSave.Write(zeroFields).SequenceEqual(originalDisabled),
        "Unabhängiger ERG-Fall: Original-Feld 0 bei inaktivem Spieler akzeptieren.");
    zeroFields.SetPlayerKind(1,PlayerKind.Computer);
    Check(zeroFields.Kinds[1]==PlayerKind.Computer&&Enumerable.Range(0,4).All(i=>zeroFields.Position(1,i)==-1),
        "Auch als Computer reaktivierte Spieler beginnen im Hof.");
    byte[] legacyInactive=(byte[])beforeKind.Clone();legacyInactive[45]=2;
    var legacyGame=OriginalSave.Read(legacyInactive);
    legacyGame.SetPlayerKind(1,PlayerKind.Human);
    Check(Enumerable.Range(0,4).All(i=>legacyGame.Position(1,i)==-1),
        "Ältere native Spielstände mit nur ausgeblendeten Figuren bleiben ladbar; Reaktivierung setzt sie in den Hof.");
    originalDisabled[45]=0;
    bool rejectedZero=false;
    try {OriginalSave.Read(originalDisabled);}catch(InvalidDataException){rejectedZero=true;}
    Check(rejectedZero,"Feld 0 bei aktivem Spieler bleibt ungültig.");
}
Console.WriteLine("OK: Spieler entfernen/neu einsetzen, freie Bahnfelder und Original-ERG-Feld 0 auf beiden Brettern.");
Console.WriteLine("OK: Zugabgleich bei Regelwechsel, Spielertypwechsel, Deaktivierung, Reaktivierung, Laden und Brettwechsel.");
{
    // seg04:0022: vorgemerkte Spieleränderungen gelten erst beim nächsten Spielerwechsel.
    var deferred = new Game();
    deferred.SetPosition(0, 0, 10); deferred.SetPosition(1, 0, 5);
    var names = (string[])deferred.Names.Clone(); names[0] = "Neu";
    var kinds = (PlayerKind[])deferred.Kinds.Clone(); kinds[1] = PlayerKind.Inactive; kinds[2] = PlayerKind.Computer;
    deferred.SetPendingPlayers(names, kinds);
    deferred.Roll(6); deferred.Move(1);
    Check(deferred.Player == 0 && deferred.Names[0] == "Spieler 1" && deferred.Kinds[1] == PlayerKind.Human &&
          deferred.Position(1, 0) == 5 && deferred.PendingKinds is not null, "Bonuswurf übernimmt vorgemerkte Spieler zu früh.");
    deferred.Roll(2); deferred.Move(1);
    Check(deferred.Names[0] == "Neu" && deferred.Kinds[1] == PlayerKind.Inactive && deferred.Kinds[2] == PlayerKind.Computer &&
          Enumerable.Range(0, 4).All(i => deferred.Position(1, i) == -1) && deferred.Player == 2 && !deferred.HasPendingChanges,
          "Spielerwechsel übernimmt vorgemerkte Spieler nicht oder gibt den Zug an den deaktivierten Spieler.");

    // Regeln: der laufende Zug nutzt noch die alten Regeln, auch beim Passen.
    var rules = new Game { Rules = new Rules(EnterOnOne: true), PendingRules = new Rules() };
    rules.Roll(1);
    Check(rules.LegalMoves().Length == 4, "Vorgemerkte Regeln wirken schon auf den laufenden Zug.");
    rules.Move(0);
    Check(!rules.Rules.EnterOnOne && rules.PendingRules is null && rules.Player == 1, "Regeln werden beim Spielerwechsel nicht übernommen.");
    var retry = new Game { PendingRules = new Rules(Capture: true) };
    retry.Roll(3); retry.Pass();
    Check(retry.Player == 0 && retry.PendingRules is not null && !retry.Rules.Capture, "Erneuter Startversuch übernimmt Regeln zu früh.");
    var pass = new Game { PendingRules = new Rules(Capture: true) };
    pass.SetPosition(0, 0, 42); pass.Roll(2); pass.Pass();
    Check(pass.Player == 1 && pass.Rules.Capture && pass.PendingRules is null, "Passen ohne Zug übernimmt vorgemerkte Regeln nicht.");

    // Brett: DS:2176 wird nach dem Wechsel erhöht und auf Spieler 1 umgebrochen.
    var grow = new Game(4); grow.RestoreTurn(3, 0, false, 0); grow.SetPosition(3, 0, 10); grow.PendingBoard = 6;
    grow.Roll(2); grow.Move(0);
    Check(grow.PlayerCount == 6 && grow.Player == 4 && !grow.BoardChangeRejected, "Wechsel auf sechs Spieler: falscher nächster Spieler.");
    var shrink = new Game(6); shrink.RestoreTurn(5, 0, false, 0); shrink.SetPosition(5, 0, 3); shrink.PendingBoard = 4;
    shrink.Roll(2); shrink.Move(0);
    Check(shrink.PlayerCount == 4 && shrink.Player == 0 && shrink.Position(5, 0) == -1, "Wechsel auf vier Spieler durch Spieler 6: falscher nächster Spieler.");
    var blocked = new Game(6); blocked.SetPosition(0, 0, 41); blocked.PendingBoard = 4;
    blocked.Roll(2); blocked.Move(0);
    Check(blocked.PlayerCount == 6 && blocked.PendingBoard is null && blocked.TakeBoardChangeRejected() && !blocked.TakeBoardChangeRejected() &&
          blocked.Player == 1 && blocked.Position(0, 0) == 43, "Blockierter Brettwechsel (seg07:042c) wird nicht gemeldet und verworfen.");

    // seg04:15ae: Spielende übernimmt alles ohne Stellungsänderung.
    var ended = new Game(); ended.SetPosition(1, 0, 7);
    ended.SetPendingPlayers(names, kinds); ended.PendingRules = new Rules(Rest: true); ended.PendingBoard = 6;
    Check(ended.ApplyPendingAfterGame() == 6 && ended.Rules.Rest && ended.Names[0] == "Neu" && ended.Kinds[1] == PlayerKind.Inactive &&
          ended.Position(1, 0) == 7 && !ended.HasPendingChanges && ended.PlayerCount == 4, "Spielende übernimmt Vormerkungen nicht.");
}
Console.WriteLine("OK: Vorgemerkte Spieler-, Regel- und Brettänderungen gelten erst beim Spielerwechsel bzw. Spielende (seg04:0022/15ae).");
{
    // seg07:003a: client point -> original field, against the extracted DS tables.
    int hitChecks = 0;
    foreach (int players in new[] { 4, 6 })
        for (int sizeIndex = 0; sizeIndex < 3; sizeIndex++)
        {
            bool six = players == 6;
            int size = BoardLayout.FieldSize(six, sizeIndex);
            Check(size == new[,] { { 14, 28, 54 }, { 10, 21, 41 } }[six ? 1 : 0, sizeIndex], "Originale Feldgröße DS:1734.");
            var track = BoardLayout.Track(six, sizeIndex);
            Check(track.Length == (six ? 48 : 40), "Originale Bahnlänge.");
            void Expect(Game state, Point origin, int field, string what)
            {
                // The field spans origin+2 .. origin+2+size in client coordinates (inclusive). On the
                // dense small boards adjacent inclusive edges overlap; there the later field wins.
                foreach (var (dx, dy) in new[] { (0, 0), (size, 0), (0, size), (size, size), (size / 2, size / 2) })
                {
                    int hit = BoardLayout.FieldAt(state, sizeIndex, new Point(origin.X + 2 + dx, origin.Y + 2 + dy));
                    bool edge = (dx, dy) != (size / 2, size / 2);
                    // Precedence track > yard > goal (later index wins): the hit field must contain the point.
                    Point? hitOrigin = hit is >= 1 and <= 48 ? track[hit - 1]
                        : hit is >= 101 and <= 104 ? BoardLayout.Origin(state, sizeIndex, state.Player, hit - 101, -1)
                        : hit is >= 201 and <= 204 ? BoardLayout.Origin(state, sizeIndex, state.Player, 0, state.TrackLength + hit - 201) : null;
                    bool overlapping = edge && hitOrigin is { } o && origin.X + dx - o.X is >= 0 && origin.X + dx - o.X <= size &&
                        origin.Y + dy - o.Y >= 0 && origin.Y + dy - o.Y <= size;
                    Check(hit == field || overlapping,
                        $"{what}: Klick auf Feld {field} ({players}er-Brett, Größe {sizeIndex + 1}, +{dx},{dy}) ergibt {hit}.");
                    hitChecks++;
                }
                Check(BoardLayout.FieldAt(state, sizeIndex, new Point(origin.X + 3 + size, origin.Y + 2 + size / 2)) != field,
                    $"{what}: Feld {field} reicht über seine Originalbreite hinaus.");
            }
            for (int current = 0; current < players; current++)
            {
                var state = new Game(players, current);
                for (int field = 0; field < track.Length; field++) Expect(state, track[field], field + 1, "Bahn");
                for (int piece = 0; piece < 4; piece++)
                {
                    Expect(state, BoardLayout.Origin(state, sizeIndex, current, piece, -1), 101 + piece, "Hof");
                    Check(BoardLayout.PieceOn(state, 101 + piece) == piece, "Hoffeld gehört nicht zur Figur.");
                    Expect(state, BoardLayout.Origin(state, sizeIndex, current, piece, state.TrackLength + piece), 201 + piece, "Ziel");
                }
                // Other players' yards are not clickable for the current player.
                int other = (current + 1) % players;
                var foreignYard = BoardLayout.Origin(state, sizeIndex, other, 0, -1);
                Check(BoardLayout.FieldAt(state, sizeIndex, new Point(foreignYard.X + 2 + size / 2, foreignYard.Y + 2 + size / 2)) == 0,
                    "Fremder Hof ist anklickbar.");
                state.SetPosition(current, 2, 7);
                Check(BoardLayout.PieceOn(state, state.Square(current, 7) + 1) == 2 && BoardLayout.PieceOn(state, state.Square(current, 8) + 1) == -1,
                    "Bahnfeld wird nicht der eigenen Figur zugeordnet.");
            }
        }
    Console.WriteLine($"OK: Klickerkennung nach seg07:003a auf allen Original-Feldern, beiden Brettern und drei Größen ({hitChecks} Treffer).");
}
strategyCase.Kinds[0]=PlayerKind.Computer;
File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory,"AIIN.ERG"),OriginalSave.Write(strategyCase));

for (int seed = 0; seed < 20; seed++)
{
    var state = new Game(6) { Rules = new Rules(Rest: true, Capture: true, Hard: seed % 2 == 0, Absolute: seed % 2 == 1, NoJump: true, EnterOnOne: true, RepeatOnOne: true) };
    var rng = new Random(seed);
    for (int turn = 0; turn < 50000 && !state.Finished; turn++)
    {
        state.Roll(rng.Next(1, 7));
        var legal = state.LegalMoves();
        if (legal.Length == 0) state.Pass(); else state.Move(legal[rng.Next(legal.Length)]);
        var occupied = new HashSet<int>();
        for (int p = 0; p < 6; p++)
            for (int i = 0; i < 4; i++)
            {
                int at = state.Position(p, i);
                Check(at is >= -1 and <= 51, "Sechserbrett: Grenzen.");
                if (at is >= 0 and < 48) Check(occupied.Add(state.Square(p, at)), "Sechserbrett: Feldbelegung.");
            }
    }
    Check(state.Winners.Count == 6 && state.Winners.Distinct().Count() == 6, "Alle sechs Plätze müssen vergeben sein.");
}
Console.WriteLine("OK: Originalhilfe-Regressionsfälle und 20 komplette Sechs-Spieler-Partien mit Sonderregeln.");

// seg13:0dc7 (state 19): if entering captures on the start field, capture
// compulsion allows every capture, not only entering.
var enterCapture=Position([5,-1,-1,-1],6,new Rules(Capture:true),(3,0,10),(1,0,1));   // opponents on squares 0 and 11
Moves(enterCapture,0,1,2,3);
Moves(Position([5,-1,-1,-1],6,new Rules(Capture:true,Hard:true),(3,0,10),(1,0,1)),0,1,2,3);
Moves(Position([5,-1,-1,-1],6,null,(3,0,10),(1,0,1)),1,2,3);
Moves(Position([5,-1,-1,-1],6,new Rules(Capture:true),(1,0,1)),1,2,3);            // empty start: entering first

// seg13:0e32 boundary: a piece on its last track field "can capture" on
// Wrap(finish+die), so capture compulsion forbids all other moves there.
var phantom=Position([39,5,-1,-1],2,new Rules(Capture:true),(3,0,11));            // opponent on absolute field 2
Check(phantom.LegalMoves().SequenceEqual(new[]{0,1}) && OriginalMoveCheck.LegalMoves(phantom).Length==0 &&
      OriginalMoveCheck.LegalMoves(phantom,false).SequenceEqual(new[]{0,1}),"Randfall seg13:0e32 (Figur auf dem letzten Bahnfeld).");

// Differential check against the direct translation of seg13:1140/043d/0e32.
var legalityRandom=new Random(20261102);
int quirkCases=0;
for(int sample=0;sample<200_000;sample++)
{
    int players=legalityRandom.Next(2)==0?4:6;
    var state=new Game(players){Rules=new Rules(legalityRandom.Next(2)==0,legalityRandom.Next(2)==0,legalityRandom.Next(2)==0,
        legalityRandom.Next(2)==0,legalityRandom.Next(2)==0,legalityRandom.Next(2)==0,legalityRandom.Next(2)==0)};
    int track=state.TrackLength,stride=track/players,me=legalityRandom.Next(players),die=legalityRandom.Next(1,7);
    var usedSquares=new HashSet<int>();
    for(int p=0;p<players;p++)
    {
        if(p!=me&&legalityRandom.Next(5)==0){state.Kinds[p]=PlayerKind.Inactive;continue;}
        var usedGoal=new HashSet<int>();
        for(int i=0;i<4;i++)
        {
            int kind=legalityRandom.Next(10);
            if(kind<3)continue;
            if(kind<5){int slot=legalityRandom.Next(4);if(usedGoal.Add(slot))state.SetPosition(p,i,track+slot);continue;}
            int distance=legalityRandom.Next(track);
            if(legalityRandom.Next(2)==0)
            {   // concentrate on start chains, finish fields and the current player's start
                int[] hot=p==me?[0,die,2*die,3*die,track-1,track-2,track-die]:[0,die,2*die,3*die,3,4];
                distance=(me*stride+hot[legalityRandom.Next(hot.Length)]-p*stride+2*track)%track;
            }
            if(usedSquares.Add(state.Square(p,distance)))state.SetPosition(p,i,distance);
        }
    }
    state.RestoreTurn(me,die,true,1);
    var native=state.LegalMoves();
    Check(native.SequenceEqual(OriginalMoveCheck.LegalMoves(state,false)),
        $"Zugprüfung weicht von seg13:043d ab: {players} Spieler, Spieler {me+1}, Wurf {die}, {state.Rules}.");
    if(!native.SequenceEqual(OriginalMoveCheck.LegalMoves(state)))
    {
        quirkCases++;
        Check(state.Rules.Capture&&Enumerable.Range(0,4).Any(i=>state.Position(me,i)==track-1),"Abweichung außerhalb des Randfalls seg13:0e32.");
    }
}
Check(quirkCases>0,"Differenztest muss den Randfall seg13:0e32 abdecken.");
Console.WriteLine($"OK: Zugprüfung identisch mit seg13:1140/043d/0e32 in 200000 Stellungen ({quirkCases} nur durch den Randfall 0e32 verschieden).");
