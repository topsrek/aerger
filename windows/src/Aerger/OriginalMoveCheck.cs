namespace Aerger;

// Direct translation of the original move check, seg13:1140, used for human
// input, passing and the computer's candidate list (seg06:0640). It collects
// three flags from the whole position and runs the nested state machine
// seg13:043d (helpers 0012 overshoot, 00da goal path, 0244 own-piece path).
// seg13:0e32 decides whether a piece can capture.
// Works in the original encoding: track 1..40/48, yard 101..104, goal 201..204.
public static class OriginalMoveCheck
{
    private const int Illegal = 0xfd, Legal = 0xfe;

    private sealed class Position
    {
        public required Game Game;
        public required int Me;            // one-based current player, DS:2176
        public required int Die;           // DS:2174
        public required int TrackLength;   // DS:1776
        public required int Start;         // DS:1777
        public required int Finish;        // DS:1783
        public required int[] Occupant;    // DS:20d4, one-based player per track field
        public required bool[] Goal;       // DS:2149, own goal slots 1..4
        public required bool[] Yard;       // DS:2131, own yard slots 1..4
        public required int[] Pieces;      // DS:20b9, own piece positions
        public required bool FinishQuirk;
        public bool CanEnter, AnyYard, AnyCapture;
        public Rules Rules => Game.Rules;
        public int Wrap(int field) => field > TrackLength ? field - TrackLength : field;   // seg07:03a5
        public bool IsRest(int field) => Game.IsRestSquare(field - 1);
        public bool IsOpponent(int player) => player != 0 && player != Me;
    }

    public static int Encode(Game game, int player, int piece)
    {
        int at = game.Position(player, piece);
        if (at < 0) return 101 + piece;
        if (at < game.TrackLength) return game.Square(player, at) + 1;
        return 201 + at - game.TrackLength;
    }

    // Pieces of the current player accepted by seg13:1140, in piece order.
    // finishQuirk reproduces the 0e32 boundary described at CanCapture.
    public static int[] LegalMoves(Game game, bool finishQuirk = true)
    {
        if (!game.AwaitingMove || game.Finished) return [];
        var position = Read(game, finishQuirk);
        return Enumerable.Range(0, 4).Where(piece => MayMove(position, position.Pieces[piece])).ToArray();
    }

    private static Position Read(Game game, bool finishQuirk)
    {
        int stride = game.TrackLength / game.PlayerCount, me = game.Player;
        var occupant = new int[game.TrackLength + 1];
        for (int p = 0; p < game.PlayerCount; p++)
            if (game.Kinds[p] != PlayerKind.Inactive)
                for (int i = 0; i < 4; i++)
                {
                    int field = Encode(game, p, i);
                    if (field <= game.TrackLength) occupant[field] = p + 1;
                }
        var pieces = Enumerable.Range(0, 4).Select(i => Encode(game, me, i)).ToArray();
        var position = new Position
        {
            Game = game, Me = me + 1, Die = game.Die, TrackLength = game.TrackLength,
            Start = me * stride + 1, Finish = me == 0 ? game.TrackLength : me * stride,
            Occupant = occupant, Pieces = pieces, FinishQuirk = finishQuirk,
            Goal = Enumerable.Range(0, 5).Select(slot => pieces.Contains(200 + slot)).ToArray(),
            Yard = Enumerable.Range(0, 5).Select(slot => pieces.Contains(100 + slot)).ToArray(),
        };
        // seg13:1247..12e4
        position.CanEnter = game.Die == 6 || (game.Die == 1 && game.Rules.EnterOnOne);
        position.AnyCapture = pieces.Any(field => CanCapture(position, field));
        position.AnyYard = position.Yard.Skip(1).Any(flag => flag);
        return position;
    }

    private static bool InYard(int field) => field is >= 101 and <= 104;
    private static bool InGoal(int field) => field is >= 201 and <= 204;

    // seg13:1140
    private static bool MayMove(Position s, int field)
    {
        if (field == 0) return false;
        if (InYard(field) && !s.Yard[field - 100]) return false;
        if (InGoal(field) && !s.Goal[field - 200]) return false;
        if (field >= 1 && field <= s.TrackLength && s.Occupant[field] != s.Me) return false;
        return Decide(s, field);
    }

    // seg13:0e32. Note the original's boundary: a piece standing exactly on its
    // last track field (field == finish) is not treated as entering the goal here,
    // so the field Wrap(finish + die) counts as a capture target (unlike 043d's
    // goal test at 04af). With capture compulsion this phantom capture forbids
    // every non-capturing move, possibly leaving no legal move at all.
    private static bool CanCapture(Position s, int field)
    {
        bool result = false, decided = false;
        if (field >= 1 && field <= s.TrackLength && !((s.FinishQuirk ? s.Finish > field : s.Finish >= field) && field + s.Die > s.Finish))
        {
            int target = s.Wrap(field + s.Die);
            if (s.IsOpponent(s.Occupant[target]) && !(s.Rules.Rest && s.IsRest(target)))
            {
                result = true;
                decided = true;
            }
        }
        if (!decided)
            result = InYard(field) && s.CanEnter && s.IsOpponent(s.Occupant[s.Start]);
        if (result && !InYard(field) && s.Rules.NoJump)
            for (int step = 1; step <= s.Die; step++)
                if (s.Occupant[s.Wrap(field + step)] == s.Me) result = false;
        return result;
    }

    // seg13:0012
    private static bool Overshoots(Position s, int field) =>
        InGoal(field) ? field + s.Die > 204 : field + s.Die - s.Finish > 4;

    private static bool GoalSlot(Position s, int slot) =>
        slot is >= 1 and <= 4 ? s.Goal[slot] : throw new InvalidOperationException("Zielfeld außerhalb der Originalprüfung.");

    // seg13:00da
    private static bool GoalPathFree(Position s, int field)
    {
        int first = InGoal(field) ? field - 200 + 1 : 1;
        int last = InGoal(field) ? field - 200 + s.Die : field + s.Die - s.Finish;
        for (int slot = first; slot <= last; slot++)
            if (GoalSlot(s, slot)) return false;
        return true;
    }

    // seg13:0244 (used with "Kein Selbstüberspringen")
    private static bool PathBlocked(Position s, int field)
    {
        if (InYard(field)) return false;
        if (InGoal(field)) return !GoalPathFree(s, field);
        bool blocked = false, inGoal = s.Finish == field;
        for (int step = 1; step <= s.Die; step++)
        {
            if (inGoal ? GoalSlot(s, field + step - s.Finish) : s.Occupant[s.Wrap(field + step)] == s.Me) blocked = true;
            if (field + step == s.Finish) inGoal = true;
        }
        return blocked;
    }

    // seg13:043d
    private static bool Decide(Position s, int field)
    {
        var rules = s.Rules;
        int start = s.Occupant[s.Start];
        bool ownStart = start == s.Me;
        bool StartChain(int f) => s.Start == f || s.Wrap(s.Start + s.Die) == f ||
            (s.Occupant[s.Wrap(s.Start + s.Die)] == s.Me && s.Wrap(s.Start + 2 * s.Die) == f);
        int state = 0;
        while (true)
        {
            switch (state)
            {
                case 0:
                    state = InYard(field) ? 1 : InGoal(field) || (s.Finish >= field && field + s.Die > s.Finish) ? 4 : 5;
                    break;
                case 1: // 0512: leaving the yard
                    state = s.Die is >= 2 and <= 5 || (s.Die == 1 && !rules.EnterOnOne) ? Illegal : 2;
                    break;
                case 2: // 053c
                    state = ownStart ? Illegal : 3;
                    break;
                case 3: // 0597: absolute capture compulsion beats entering an empty start
                    state = start == 0 && rules.Capture && rules.Absolute && s.AnyCapture ? Illegal : Legal;
                    break;
                case 4: // 0607: goal entry or move inside the goal
                    state = Overshoots(s, field) || (rules.NoJump && PathBlocked(s, field)) || !GoalPathFree(s, field) ||
                            (s.AnyCapture && rules.Capture) || (s.AnyYard && s.CanEnter) || (s.AnyYard && ownStart)
                        ? Illegal : Legal;
                    break;
                case 5: // 06b6: track move
                {
                    int target = s.Wrap(field + s.Die), occupant = s.Occupant[target];
                    if (occupant == s.Me || (rules.Rest && s.IsRest(target) && occupant != 0) ||
                        (rules.NoJump && PathBlocked(s, field))) state = Illegal;
                    else state = occupant != 0 ? 6 : 12;
                    break;
                }
                case 6: // 07f6: capture on the track
                    if (s.CanEnter && s.AnyYard && s.IsOpponent(start)) state = 19;
                    else if (s.CanEnter && s.AnyYard && start == 0 && !(rules.Capture && rules.Absolute)) state = Illegal;
                    else if (s.AnyYard && ownStart) state = 7;
                    else state = 11;
                    break;
                case 7: case 8: case 9: // 097f..0b0a: piece clearing the start (two levels)
                    state = StartChain(field) ? 11 : 10;
                    break;
                case 10: // 0b0d
                    state = rules.Capture && (rules.Hard || rules.Absolute) ? 11 : Illegal;
                    break;
                case 11: case 17: // 0b33, 0d76
                    state = rules.NoJump && PathBlocked(s, field) ? Illegal : Legal;
                    break;
                case 12: // 0b53: ordinary move
                    state = rules.Capture && (rules.Hard || rules.Absolute) && s.AnyCapture ? Illegal : 13;
                    break;
                case 13: // 0b83
                    state = s.AnyYard && ownStart ? 14 : 18;
                    break;
                case 14: case 15: case 16: // 0be8..0d73
                    state = StartChain(field) ? 17 : Illegal;
                    break;
                case 18: // 0d95
                    state = (s.AnyYard && s.CanEnter) || (rules.Capture && s.AnyCapture) ? Illegal : 17;
                    break;
                case 19: // 0dc7: entering would capture too; any capture may be chosen
                    state = rules.Capture ? Legal : Illegal;
                    break;
                case Illegal: return false;
                case Legal: return true;
                default: throw new InvalidOperationException($"Unbekannter Zustand {state}.");
            }
        }
    }
}
