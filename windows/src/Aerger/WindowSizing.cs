using System.Drawing;

namespace Aerger;

internal static class WindowSizing
{
    // seg09:02c7: player-panel right edge + 4, board height + 4,
    // with the current Windows frame, caption and menu added separately.
    internal static Size ClientForBoard(int boardSize) => boardSize switch
    {
        200 => new(255, 204),
        400 => new(501, 404),
        700 => new(860, 704),
        _ => throw new ArgumentOutOfRangeException(nameof(boardSize))
    };

    // seg09:09b0 checks both dimensions, descending from size 3 to 1.
    internal static int LargestFittingBoard(Size availableClient)
    {
        foreach (int board in new[] { 700, 400, 200 })
        {
            var required = ClientForBoard(board);
            if (required.Width <= availableClient.Width && required.Height <= availableClient.Height)
                return board;
        }
        return 200;
    }
}
