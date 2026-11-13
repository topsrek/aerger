using System.Drawing;

namespace Aerger;

/// <summary>Original board geometry per board size (OriginalBoard.g.cs, extracted from DS):
/// seg17:0291 draws the board bitmap at (2,2), seg17:04c1 draws a piece at its field origin + 2,
/// and seg07:003a maps a click back to an original field number.</summary>
public static class BoardLayout
{
    public const int Margin = 2;
    public static int SizeIndex(int boardSize) => boardSize == 200 ? 0 : boardSize == 400 ? 1 : 2;
    public static int FieldSize(bool six, int sizeIndex) => OriginalBoard.FieldSize[six ? 1 : 0, sizeIndex];
    /// <summary>Field origins of absolute track fields 1..40/48 (index 0-based).</summary>
    public static Point[] Track(bool six, int sizeIndex) => (six ? OriginalBoard.TrackSix : OriginalBoard.TrackFour)[sizeIndex];
    public static Point[] RestCorners(bool six, int sizeIndex) => (six ? OriginalBoard.RestCornersSix : OriginalBoard.RestCornersFour)[sizeIndex];
    static Point[] Yard(bool six, int sizeIndex, int player) => (six ? OriginalBoard.YardSix : OriginalBoard.YardFour)[sizeIndex][player];
    static Point[] Goal(bool six, int sizeIndex, int player) => (six ? OriginalBoard.GoalSix : OriginalBoard.GoalFour)[sizeIndex][player];

    /// <summary>Field origin (without margin) for a piece at a native position: yard slot = piece number
    /// (ERG 101..104), track = absolute field, goal = slot 201..204.</summary>
    public static Point Origin(Game game, int sizeIndex, int player, int piece, int position)
    {
        bool six = game.PlayerCount == 6;
        if (position < 0) return Yard(six, sizeIndex, player)[piece];
        if (position < game.TrackLength) return Track(six, sizeIndex)[game.Square(player, position)];
        return Goal(six, sizeIndex, player)[position - game.TrackLength];
    }
    /// <summary>Top-left client position of a piece sprite (seg17:04c1: field origin + 2).</summary>
    public static Point PieceAt(Game game, int sizeIndex, int player, int piece)
        => PieceAt(game, sizeIndex, player, piece, game.Position(player, piece));
    public static Point PieceAt(Game game, int sizeIndex, int player, int piece, int position)
    {
        var origin = Origin(game, sizeIndex, player, piece, position);
        return new(origin.X + Margin, origin.Y + Margin);
    }
    /// <summary>Client point in the middle of a piece's field.</summary>
    public static Point Center(Game game, int sizeIndex, int player, int piece)
    {
        var at = PieceAt(game, sizeIndex, player, piece);
        int half = FieldSize(game.PlayerCount == 6, sizeIndex) / 2;
        return new(at.X + half, at.Y + half);
    }

    /// <summary>seg07:003a: client point to original field number (1..48 track, 101..104 yard and
    /// 201..204 goal of the current player, 0 none). Coordinates above 2 lose the margin; a field
    /// spans origin..origin+size inclusive; the last matching track field wins, then yard, then goal.</summary>
    public static int FieldAt(Game game, int sizeIndex, Point client)
    {
        bool six = game.PlayerCount == 6;
        int x = client.X > 2 ? client.X - 2 : client.X, y = client.Y > 2 ? client.Y - 2 : client.Y;
        int size = FieldSize(six, sizeIndex);
        bool Hit(Point origin) => x - origin.X >= 0 && x - origin.X <= size && y - origin.Y >= 0 && y - origin.Y <= size;
        int found = 0;
        var track = Track(six, sizeIndex);
        for (int field = 0; field < track.Length; field++) if (Hit(track[field])) found = field + 1;
        if (found != 0) return found;
        var yard = Yard(six, sizeIndex, game.Player);
        for (int slot = 0; slot < 4; slot++) if (Hit(yard[slot])) found = 101 + slot;
        if (found != 0) return found;
        var goal = Goal(six, sizeIndex, game.Player);
        for (int slot = 0; slot < 4; slot++) if (Hit(goal[slot])) found = 201 + slot;
        return found;
    }
    /// <summary>The current player's piece on an original field number, or -1 (seg13:1140 rejects others).</summary>
    public static int PieceOn(Game game, int field)
    {
        for (int piece = 0; piece < 4; piece++)
        {
            int position = game.Position(game.Player, piece);
            int code = position < 0 ? 101 + piece : position < game.TrackLength ? game.Square(game.Player, position) + 1 : 201 + position - game.TrackLength;
            if (code == field) return piece;
        }
        return -1;
    }
}
