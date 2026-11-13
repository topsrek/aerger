using System.Text.Json;

namespace Aerger;

public sealed class OriginalDialog : Form
{
    private readonly List<(int Id, Control Control)> fields = [];
    private readonly List<(Rectangle Bounds,string Text,int Style)> shades=[];
    private readonly bool automated;
    protected override bool ShowWithoutActivation=>automated;
    public string Resource { get; }
    /// <summary>WS_CAPTION of the resource; without it the dialog has no title bar.</summary>
    internal bool HasCaption { get; }
    public OriginalDialog(string resource,bool automated=false)
    {
        this.automated=automated;
        Resource=resource;
        using var stream = typeof(OriginalDialog).Assembly.GetManifestResourceStream("Aerger.Reference.dialogs.json")!;
        using var document = JsonDocument.Parse(stream);
        var template = document.RootElement.GetProperty(resource);
        Text = template.GetProperty("title").GetString();
        Font = new Font("Microsoft Sans Serif", 9.75f, FontStyle.Bold);
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new(template.GetProperty("width").GetInt32() * 2, template.GetProperty("height").GetInt32() * 2);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        // Window style of the resource: no dialog has WS_SYSMENU, so there is no close box;
        // DLG_NEU_FRAGE, DLG_SPIELERAENDERUNG and DLG_KEINUMSCHALTEN have no WS_CAPTION
        // (dialog frame only). Escape and the dialog's own buttons still close them.
        long windowStyle = template.GetProperty("style").GetInt64();
        HasCaption = (windowStyle & 0x00C00000) == 0x00C00000;
        ControlBox = (windowStyle & 0x00080000) != 0;
        if (!HasCaption) Text = "";
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(192, 192, 192);
        int tabIndex=0;
        foreach (var field in template.GetProperty("controls").EnumerateArray())
        {
            int order=tabIndex++;
            string kind = field.GetProperty("cls").GetString()!;
            string text = field.GetProperty("text").GetString()!;
            int id = field.GetProperty("id").GetInt32();
            if(kind=="BorShade")
            {
                shades.Add((new Rectangle(field.GetProperty("x").GetInt32()*2,field.GetProperty("y").GetInt32()*2,
                    field.GetProperty("width").GetInt32()*2,field.GetProperty("height").GetInt32()*2),text,(int)(field.GetProperty("style").GetInt64()&0xFFFF)));
                continue;
            }
            Control control;
            if (kind == "BorCheck") control = new BwccCheckBox { Text = text, AutoSize = false, FlatStyle = FlatStyle.Standard };
            else if (kind == "BorRadio")
            {
                var radio = new BwccRadioButton { Text = text, AutoCheck = false, Group = resource is "DLG_SPIELER" or "DLG_SPEED" ? (id-101)/4 : 0 };
                radio.Click += (_, _) => SelectRadio(radio);
                radio.MoveSelection=direction=>MoveRadio(radio,direction);
                control = radio;
            }
            // Edit style 0x50810000: WS_BORDER, a 1-pixel frame in the window-frame colour (screenshot: 100,100,100).
            else if (kind == "Edit") control = new Win16TextBox { MaxLength = 20, BorderStyle = BorderStyle.FixedSingle, AutoSize = false };
            else if (kind == "BorBtn")
            {
                int sample=id==7?7:id==6?6:text=="Schalter"?(id==2?2:1):0;
                string caption=sample switch {1=>"&OK",2=>"&Abbrechen",6=>"&Ja",7=>"&Nein",_=>text};
                var button = new BwccButton { Text = caption, AccessibleName=caption.Replace("&",""), Sample=sample,
                    UseVisualStyleBackColor = false,
                    DialogResult = id switch {1=>DialogResult.OK,2=>DialogResult.Cancel,6=>DialogResult.Yes,7=>DialogResult.No,101 when resource=="DLG_GEWINNER"=>DialogResult.Abort,_=>DialogResult.None} };
                if (id == 1) AcceptButton = button;
                if (id is 2 or 7) CancelButton = button;
                control = button;
            }
            else if (kind == "BorShade") control = new Panel { BorderStyle = BorderStyle.Fixed3D };
            else
            {
                // Static style bits 0..3: SS_LEFT 0, SS_CENTER 1, SS_RIGHT 2 (e.g. DLG_GEWINNER names are centred).
                var align=(field.GetProperty("style").GetInt64()&0xF) switch {1=>ContentAlignment.MiddleCenter,2=>ContentAlignment.MiddleRight,_=>ContentAlignment.MiddleLeft};
                control = new BwccStatic { Text = text, AutoSize = false, TextAlign = align,BackColor=Color.Transparent,
                    NoPrefix=(field.GetProperty("style").GetInt64()&0x80)!=0 };
            }
            control.SetBounds(field.GetProperty("x").GetInt32() * 2, field.GetProperty("y").GetInt32() * 2,
                field.GetProperty("width").GetInt32() * 2, field.GetProperty("height").GetInt32() * 2);
            control.TabIndex=order;
            control.TabStop=(field.GetProperty("style").GetInt64()&0x10000)!=0;
            fields.Add((id, control));
            Controls.Add(control);
            if (kind == "BorShade") control.SendToBack(); else control.BringToFront();
        }
        foreach(var group in fields.Select(f=>f.Control).OfType<BwccRadioButton>().GroupBy(radio=>radio.Group))
            SelectRadio(group.First());
    }
    private IEnumerable<BwccRadioButton> RadioGroup(BwccRadioButton radio) =>
        fields.Select(field=>field.Control).OfType<BwccRadioButton>().Where(other=>other.Group==radio.Group);
    private void SelectRadio(BwccRadioButton radio)
    {
        foreach(var other in RadioGroup(radio))
        {other.Checked=other==radio;other.TabStop=other==radio;}
    }
    private void MoveRadio(BwccRadioButton radio,int direction)
    {
        var choices=RadioGroup(radio).Where(other=>other.Enabled&&other.Visible).ToArray();
        if(choices.Length==0)return;
        int index=Array.IndexOf(choices,radio);
        var next=choices[(index+direction+choices.Length)%choices.Length];
        SelectRadio(next);next.Select();
    }
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if(keyData==Keys.Escape&&CancelButton is null)
        {DialogResult=DialogResult.Cancel;Close();return true;}
        return base.ProcessDialogKey(keyData);
    }
    private static readonly Pen ShadeDark=new(Color.FromArgb(128,128,128));
    /// <summary>BWCC BorShade in the classic colours (dark grey 128, white; Windows 10 ControlDark
    /// would be 160). Style (BWCC.H): 1 recessed group, 6 raised group, 2/3 horizontal/vertical dip,
    /// 4/5 horizontal/vertical bump; 0x8000 caption. Checked against the original screenshots.</summary>
    internal static void DrawShade(Graphics g,Rectangle r,string text,int style)
    {
        Pen Top(bool raised)=>raised?Pens.White:ShadeDark;
        Pen Bottom(bool raised)=>raised?ShadeDark:Pens.White;
        switch(style&0xFF)
        {
            case 1 or 6 when (style&0x8000)!=0&&text.Length>0:
                // Caption: grey field with a ridge under it (white on its last row, dark one row below).
                g.FillRectangle(Brushes.Silver,r);
                g.DrawLine(Pens.White,r.Left+1,r.Bottom-1,r.Right,r.Bottom-1);
                g.DrawLine(ShadeDark,r.Left,r.Bottom,r.Right-2,r.Bottom);
                // The caption text starts 2 px in, at the top of the field.
                Win16Text.Draw(g,text,new Rectangle(r.Left+2,r.Top,r.Width-2,r.Height),Color.Black,
                    TextFormatFlags.Left|TextFormatFlags.Top|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
                return;
            case 1 or 6:
                bool raised=(style&0xFF)==6;
                g.FillRectangle(Brushes.Silver,r);
                g.DrawLine(Top(raised),r.Left,r.Top,r.Right-1,r.Top);g.DrawLine(Top(raised),r.Left,r.Top,r.Left,r.Bottom-1);
                g.DrawLine(Bottom(raised),r.Left+1,r.Bottom-1,r.Right-1,r.Bottom-1);g.DrawLine(Bottom(raised),r.Right-1,r.Top+1,r.Right-1,r.Bottom-1);
                break;
            case 2 or 4:
                g.DrawLine(Top(style==4),r.Left,r.Top,r.Right-1,r.Top);
                g.DrawLine(Bottom(style==4),r.Left,r.Top+1,r.Right-1,r.Top+1);
                break;
            case 3 or 5:
                g.DrawLine(Top(style==5),r.Left,r.Top,r.Left,r.Bottom-1);
                g.DrawLine(Bottom(style==5),r.Left+1,r.Top,r.Left+1,r.Bottom-1);
                break;
        }
        if(text.Length>0)Win16Text.Draw(g,text,r,Color.Black,
            TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
    }
    // BWCC dialog background (BWCC.DLL bitmap 998). In the original screenshots its white dots
    // fall on the other column parity than with a tile anchored at the client origin.
    private static readonly TextureBrush Background=new(BwccGraphics.Image(998)){Transform=new System.Drawing.Drawing2D.Matrix(1,0,0,1,1,0)};
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.FillRectangle(Background,ClientRectangle);
        // BorDlg's raised 1-pixel edge along the client area (screenshots: white top/left, 128 grey right/bottom).
        var r=ClientRectangle;
        e.Graphics.DrawLine(Pens.White,0,0,r.Right-1,0);e.Graphics.DrawLine(Pens.White,0,0,0,r.Bottom-1);
        e.Graphics.DrawLine(ShadeDark,r.Right-1,0,r.Right-1,r.Bottom-1);e.Graphics.DrawLine(ShadeDark,0,r.Bottom-1,r.Right-1,r.Bottom-1);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // The original shows the DLG_SPIELER separator dips in all six panels, whether the dip comes
        // before or after its group box in the resource: group fields first, then dips and bumps.
        foreach(var shade in shades.OrderBy(shade=>(shade.Style&0xFF) is 1 or 6?0:1))
            DrawShade(e.Graphics,shade.Bounds,shade.Text,shade.Style);
        // The default button's black frame is one pixel larger than the button window: its right and
        // bottom line lie on the dialog (original screenshots).
        foreach(var button in Controls.OfType<BwccButton>().Where(button=>button.ShowsDefault&&button.Sample>0))
        {
            e.Graphics.DrawLine(Pens.Black,button.Right,button.Top,button.Right,button.Bottom);
            e.Graphics.DrawLine(Pens.Black,button.Left,button.Bottom,button.Right,button.Bottom);
        }
    }
    public void Check(int id, bool value)
    {
        foreach (var field in fields.Where(f => f.Id == id))
            if (field.Control is CheckBox check) check.Checked = value;
            else if (field.Control is BwccRadioButton radio)
            {if(value)SelectRadio(radio);else radio.Checked=false;}
    }
    internal Control Field(int id)=>fields.First(field=>field.Id==id).Control;
    /// <summary>Static texts, check boxes and radio buttons of the original resource.</summary>
    internal IEnumerable<Control> Texts=>fields.Select(field=>field.Control).Where(control=>control is BwccStatic or BwccCheckBox or BwccRadioButton);
    internal static IReadOnlyList<string> Resources()
    {
        using var stream=typeof(OriginalDialog).Assembly.GetManifestResourceStream("Aerger.Reference.dialogs.json")!;
        using var document=JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject().Select(property=>property.Name).ToArray();
    }
    public bool Checked(int id) => fields.Any(f => f.Id == id && (f.Control is CheckBox c && c.Checked || f.Control is RadioButton r && r.Checked));
    public void Value(int id, string value) { foreach (var field in fields.Where(f => f.Id == id && f.Control is not Panel)) field.Control.Text = value; }
    public string Value(int id) => fields.First(f => f.Id == id && f.Control is not Panel).Control.Text;
}
