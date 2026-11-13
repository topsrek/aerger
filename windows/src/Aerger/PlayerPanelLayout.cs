using System.Drawing;

namespace Aerger;

internal readonly record struct PlayerPanelLayout(Rectangle Bounds, Point FourPlayerFigure,
    Point SixPlayerFigure, Point Name, int FontPixels, Point Die,
    Point Copyright, int CopyrightFontPixels, int CopyrightLineHeight)
{
    internal static PlayerPanelLayout ForBoard(int boardSize) => boardSize switch
    {
        200 => new(new(205,2,46,50),new(219,14),new(221,14),new(227,39),8,new(220,80),new(228,120),12,15),
        400 => new(new(405,2,92,100),new(437,27),new(441,27),new(452,77),12,new(439,160),new(454,240),20,30),
        700 => new(new(705,2,151,150),new(752,32),new(759,32),new(782,110),24,new(754,300),new(782,460),36,45),
        _ => throw new ArgumentOutOfRangeException(nameof(boardSize))
    };
}
