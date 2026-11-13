using System.Runtime.InteropServices;

namespace Aerger;

/// <summary>The dialogs have no FONT statement, so their controls use the System raster font
/// (vgasys.fon, still the SYSTEM_FONT stock object), as the original screenshots show. GDI+ and
/// TextRenderer accept only TrueType fonts, so dialog text is drawn with GDI DrawText.</summary>
internal static class Win16Text
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct TEXTMETRIC
    {
        public int tmHeight, tmAscent, tmDescent, tmInternalLeading, tmExternalLeading, tmAveCharWidth, tmMaxCharWidth,
            tmWeight, tmOverhang, tmDigitizedAspectX, tmDigitizedAspectY;
        public char tmFirstChar, tmLastChar, tmDefaultChar, tmBreakChar;
        public byte tmItalic, tmUnderlined, tmStruckOut, tmPitchAndFamily, tmCharSet;
    }
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int index);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint hdc, nint obj);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint hdc, int mode);
    [DllImport("gdi32.dll")] private static extern int SetTextColor(nint hdc, int color);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint hdc);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int GetTextFaceW(nint hdc, int count, char[] face);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool GetTextMetricsW(nint hdc, out TEXTMETRIC metric);
    [DllImport("user32.dll")] private static extern bool DrawFocusRect(nint hdc, ref RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawTextW(nint hdc, string text, int length, ref RECT rect, uint format);
    internal static readonly nint Font = GetStockObject(13);   // SYSTEM_FONT
    private static readonly nint measureDc = CreateCompatibleDC(0);
    private const uint CalcRect = 0x400;
    // TextFormatFlags share the DT_* values in the low bits; the rest are TextRenderer options.
    private static uint Format(TextFormatFlags flags) => (uint)flags & 0xFFFFF;
    internal static Size Measure(string text, int width, TextFormatFlags flags)
    {
        if (text.Length == 0) return Size.Empty;
        var old = SelectObject(measureDc, Font);
        var rect = new RECT { Right = width > 0 ? width : 0x7FFF };
        DrawTextW(measureDc, text, -1, ref rect, Format(flags) | CalcRect);
        SelectObject(measureDc, old);
        return new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
    internal static void Draw(Graphics g, string text, Rectangle bounds, Color color, TextFormatFlags flags)
    {
        var hdc = g.GetHdc();
        try
        {
            var old = SelectObject(hdc, Font);
            SetBkMode(hdc, 1);
            SetTextColor(hdc, ColorTranslator.ToWin32(color));
            var rect = new RECT { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
            DrawTextW(hdc, text, -1, ref rect, Format(flags));
            SelectObject(hdc, old);
        }
        finally { g.ReleaseHdc(hdc); }
    }
    /// <summary>Win32 DrawFocusRect (XOR dots, (63,63,63) on the grey background as in the original).</summary>
    internal static void DrawFocus(Graphics g, Rectangle bounds)
    {
        var hdc = g.GetHdc();
        try { var rect = new RECT { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom }; DrawFocusRect(hdc, ref rect); }
        finally { g.ReleaseHdc(hdc); }
    }
    /// <summary>Face name and metrics of the font (the dialog test requires the raster font).</summary>
    internal static (string Face, TEXTMETRIC Metric) Describe()
    {
        var old = SelectObject(measureDc, Font);
        var face = new char[64];
        int length = GetTextFaceW(measureDc, face.Length, face);
        GetTextMetricsW(measureDc, out var metric);
        SelectObject(measureDc, old);
        return (new string(face, 0, Math.Max(0, length - 1)), metric);
    }
}

/// <summary>Edit control of an original dialog, with the dialog's raster font.</summary>
internal sealed class Win16TextBox : TextBox
{
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint hwnd, int message, nint wParam, nint lParam);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SendMessageW(Handle, 0x30, Win16Text.Font, 1);   // WM_SETFONT
    }
}


internal static class BwccGraphics
{
    private static readonly Dictionary<int,Bitmap> images=[];
    internal static Bitmap Image(int id)
    {
        if(!images.TryGetValue(id,out var bitmap))
        {
            using var stream=typeof(BwccGraphics).Assembly.GetManifestResourceStream($"Aerger.Reference.Bwcc.{id}.png")!;
            using var source=new Bitmap(stream);
            bitmap=new Bitmap(source);images.Add(id,bitmap);
        }
        return bitmap;
    }
    /// <summary>Draws the focus frame on this control without keyboard focus (inactive test dialogs).</summary>
    internal static Control? ShowFocusForTest;
    internal const TextFormatFlags ChoiceText=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine;
    internal static Rectangle ChoiceTextBounds(Control c,bool radio)
    {
        var glyph=Image(radio?130:110);
        // Original screenshots: the text starts 6 px right of the check box or radio glyph.
        return new Rectangle(glyph.Width+6,0,c.Width-glyph.Width-6,c.Height);
    }
    internal static void Choice(Graphics g,Control c,bool check,bool radio)
    {
        g.Clear(c.BackColor);
        var glyph=Image((radio?130:110)+(check?1:0));
        g.DrawImageUnscaled(glyph,0,(c.Height-glyph.Height)/2);
        var textBounds=ChoiceTextBounds(c,radio);
        Win16Text.Draw(g,c.Text,textBounds,c.Enabled?c.ForeColor:SystemColors.GrayText,ChoiceText);
        // Original screenshot: the dotted focus frame hugs the text (extent + 3 px, from 1 px left of
        // the text) over the full control height, not the whole area right of the glyph.
        if(c.Focused||ShowFocusForTest==c)
        {
            int width=Math.Min(textBounds.Width+1,Win16Text.Measure(c.Text.TrimEnd(),0,ChoiceText).Width+3);
            Win16Text.DrawFocus(g,new Rectangle(textBounds.X-1,0,width,c.Height-1));
        }
    }
    /// <summary>Text drawn by an original dialog control fits its resource bounds without clipping
    /// (trailing blanks such as in DLG_SIZE "Mittel " are invisible and may be cut).</summary>
    internal static bool TextFits(Control c)
    {
        if(c is BwccStatic text)
        {
            var size=Win16Text.Measure(text.Text.TrimEnd(),text.Width,text.Flags);
            return size.Width<=text.Width&&size.Height<=text.Height;
        }
        if(c is BwccCheckBox or BwccRadioButton)
        {
            var bounds=ChoiceTextBounds(c,c is BwccRadioButton);
            var size=Win16Text.Measure(c.Text.TrimEnd(),bounds.Width,ChoiceText);
            return size.Width<=bounds.Width&&size.Height<=bounds.Height;
        }
        return true;
    }
}

/// <summary>Win16 static text (SS_LEFT: DrawText with DT_WORDBREAK, no GDI+ text padding), vertically
/// centred in its resource bounds. Measuring and drawing share the same flags.</summary>
internal sealed class BwccStatic : Label
{
    /// <summary>SS_NOPREFIX (0x80): show '&' literally instead of underlining the next character.</summary>
    internal bool NoPrefix {get;init;}
    internal TextFormatFlags Flags=>TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|(NoPrefix?TextFormatFlags.NoPrefix:0)|
        (TextAlign is ContentAlignment.MiddleCenter or ContentAlignment.TopCenter or ContentAlignment.BottomCenter?TextFormatFlags.HorizontalCenter:TextFormatFlags.Left);
    protected override void OnPaint(PaintEventArgs e)
    {
        var size=Win16Text.Measure(Text,Width,Flags);
        var bounds=new Rectangle(0,Math.Max(0,(Height-size.Height)/2),Width,Math.Min(Height,size.Height));
        Win16Text.Draw(e.Graphics,Text,bounds,Enabled?ForeColor:SystemColors.GrayText,Flags);
    }
}

internal sealed class BwccCheckBox : CheckBox
{
    public BwccCheckBox()=>SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    protected override void OnPaint(PaintEventArgs e)=>BwccGraphics.Choice(e.Graphics,this,Checked,false);
}
internal sealed class BwccRadioButton : RadioButton
{
    internal int Group {get;init;}
    internal Action<int>? MoveSelection {get;set;}
    public BwccRadioButton()=>SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    protected override void OnPaint(PaintEventArgs e)=>BwccGraphics.Choice(e.Graphics,this,Checked,true);
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if(keyData is Keys.Left or Keys.Up or Keys.Right or Keys.Down)
        {MoveSelection?.Invoke(keyData is Keys.Left or Keys.Up?-1:1);return true;}
        return base.ProcessDialogKey(keyData);
    }
}
internal sealed class BwccButton : Button
{
    internal int Sample { get; init; }
    private bool pressed;
    internal bool ShowsDefault=>IsDefault;
    public override void NotifyDefault(bool value)
    {
        base.NotifyDefault(value);
        Invalidate();
        // The dialog draws the outer right/bottom line of the default frame (see OriginalDialog.OnPaint).
        Parent?.Invalidate(new Rectangle(Left,Top,Width+1,Height+1));
    }
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
    protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Sample>0)
        {
            e.Graphics.Clear(BackColor);
            // BWCC: ID+1000 normal, ID+3000 gedrückt, ID+5000 mit Fokus.
            var image=BwccGraphics.Image((pressed?3000:Focused?5000:1000)+Sample);
            // Original screenshots: BWCC draws the 63x39 bitmap at (1,1) of the 64x40 button; the default
            // push button (BS_DEFPUSHBUTTON, e.g. OK) gets a second black frame around it.
            if(IsDefault)e.Graphics.DrawRectangle(Pens.Black,0,0,Width-1,Height-1);
            e.Graphics.DrawImageUnscaled(image,(Width-image.Width)/2+1,(Height-image.Height)/2+1);
        }
        else
        {
            e.Graphics.Clear(Color.Silver);
            ControlPaint.DrawBorder3D(e.Graphics,ClientRectangle,pressed?Border3DStyle.Sunken:Border3DStyle.Raised);
            Win16Text.Draw(e.Graphics,Text,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
            if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
        }
    }
}
