using System.Runtime.InteropServices;

namespace Aerger;

internal static class DialogInteractionTest
{
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint window,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
    private static void Check(bool condition,string message)
    {if(!condition)throw new Exception(message);}
    private static readonly Dictionary<string,(string Name,int Limit)> Screenshots=new()
    {
        ["DLG_REGELN"]=("original-rules",60),["DLG_SPEED"]=("original-speed",60),["DLG_SPIELER"]=("original-players-filled",150),
    };
    /// <summary>Height and width of each band of dark text pixels right of the check boxes (x &gt;= 45),
    /// below <paramref name="top"/>; taller bands (buttons) are left out.</summary>
    private static List<(int Height,int Width)> TextRows(Bitmap picture,int top)
    {
        var rows=new List<(int Height,int Width)>();
        int start=-1,left=int.MaxValue,right=-1;
        for(int y=top;y<=picture.Height;y++)
        {
            int rowLeft=int.MaxValue,rowRight=-1;
            for(int x=45;y<picture.Height&&x<picture.Width-10;x++)
            {
                var pixel=picture.GetPixel(x,y);
                if(pixel.R+pixel.G+pixel.B<150){rowLeft=Math.Min(rowLeft,x);rowRight=x;}
            }
            if(rowRight>=0){if(start<0){start=y;left=rowLeft;right=rowRight;}else{left=Math.Min(left,rowLeft);right=Math.Max(right,rowRight);}}
            else if(start>=0){if(y-start<20)rows.Add((y-start,right-left+1));start=-1;}
        }
        return rows;
    }

    // Use the same preprocessing and window-message dispatch as WinForms' input
    // loop, with an explicit target and modifier state for a background dialog.
    internal static void Key(Control target,Keys key)
    {
        var state=new byte[256];
        if((key&Keys.Shift)!=0)state[0x10]=0x80;
        if((key&Keys.Control)!=0)state[0x11]=0x80;
        if((key&Keys.Alt)!=0)state[0x12]=0x80;
        Check(SetKeyboardState(state),"Dialog-Testtastatur konnte nicht gesetzt werden.");
        try
        {
            var code=(nuint)(key&Keys.KeyCode);
            var message=Message.Create(target.Handle,0x100,(nint)code,1);
            if(!target.PreProcessMessage(ref message))SendMessageW(target.Handle,0x100,code,1);
            SendMessageW(target.Handle,0x101,code,unchecked((nint)0xc0000001));
        }
        finally {SetKeyboardState(new byte[256]);}
    }

    internal static void Run(GameWindow window)
    {
        // The control-only checks use modeless forms and pump queued messages.
        // Keep the board idle so an ambient Space/Enter cannot start a roll
        // while those forms intentionally leave their owner enabled.
        Check(!window.TestBusy,$"Dialogtests beginnen während einer Animation: {window.TestInputState}.");
        var initialState=window.TestState;
        byte[] initialBytes=OriginalSave.Write(initialState);
        window.TestStopGame();
        using(var speed=new OriginalDialog("DLG_SPEED",true))
        {
            speed.Check(102,true);speed.Check(106,true);
            speed.Show(window);
            Key(window,Keys.Space);Key(window,Keys.Enter);
            Application.DoEvents();
            Check(!window.TestBusy,"Tastatur im Hintergrund startet während der Dialogvorbereitung eine Animation.");
            speed.ActiveControl=speed.Field(102);
            Key(speed.Field(102),Keys.Right);
            Check(speed.Checked(103)&&!speed.Checked(102)&&speed.Checked(106),"Pfeiltaste ändert die falsche Geschwindigkeitsgruppe.");
            Key(speed.Field(103),Keys.Right);Key(speed.Field(104),Keys.Right);
            Check(speed.Checked(101)&&speed.Checked(106),"Radioauswahl läuft über die Gruppengrenze statt umzubrechen.");
            Key(speed.Field(101),Keys.Left);
            Check(speed.Checked(104)&&speed.Checked(106),"Rückwärtsnavigation bricht nicht innerhalb der Gruppe um.");
            speed.Field(101).Enabled=false;
            Key(speed.Field(104),Keys.Right);
            Check(speed.Checked(102),"Radio-Navigation wählt einen deaktivierten Eintrag.");
            Key(speed.Field(102),Keys.Tab);
            Check(speed.ActiveControl==speed.Field(106),"Tab erreicht nicht die zweite Geschwindigkeitsgruppe.");
            Key(speed.Field(106),Keys.Shift|Keys.Tab);
            Check(speed.ActiveControl==speed.Field(102),"Umschalt+Tab verliert die ausgewählte Geschwindigkeitsstufe.");
            Key(speed.Field(102),Keys.Space);
            Check(speed.Checked(102)&&speed.Checked(106),"Leertaste hebt eine Radioauswahl auf.");
            speed.Check(103,true);
            Check(!speed.Checked(102)&&speed.Checked(103)&&speed.Checked(106),"Programmatische Radioauswahl ist nicht exklusiv.");
            speed.Close();
        }
        using(var players=new OriginalDialog("DLG_SPIELER",true))
        {
            for(int player=0;player<6;player++)players.Check(102+player*4,true);
            players.Show(window);Application.DoEvents();
            players.ActiveControl=players.Field(102);
            Key(players.Field(102),Keys.Right);
            Check(players.Checked(103)&&!players.Checked(102)&&players.Checked(106),"Spielertypen verschiedener Spieler beeinflussen sich.");
            Key(players.Field(103),Keys.Right);Key(players.Field(104),Keys.Right);
            Check(players.Checked(102)&&players.Checked(106),"Spielertyp-Navigation verlässt die Spielergruppe.");
            players.Close();
        }
        using(var sound=new OriginalDialog("DLG_SOUND",true))
        {
            sound.Show(window);Application.DoEvents();
            sound.ActiveControl=sound.Field(101);
            Key(sound.Field(101),Keys.Space);
            Check(sound.Checked(101)&&!sound.Checked(102),"Leertaste schaltet das Kontrollkästchen nicht unabhängig um.");
            Key(sound.Field(101),Keys.Space);
            Check(!sound.Checked(101),"Kontrollkästchen lässt sich nicht wieder ausschalten.");
            sound.Close();
        }

        // Dialogs without a FONT statement use the System raster font (vgasys.fon).
        var (face,metric)=Win16Text.Describe();
        Check(face=="System"&&(metric.tmPitchAndFamily&6)==0&&metric.tmHeight==16&&metric.tmWeight==700,
            $"Dialogschrift ist nicht die System-Rasterschrift: {face}, Höhe {metric.tmHeight}, Gewicht {metric.tmWeight}, Familie {metric.tmPitchAndFamily:X}.");
        var screenshotReport=new System.Text.StringBuilder();
        // Every original static text must fit its resource bounds (e.g. DLG_SPIELERAENDERUNG).
        foreach(var resource in OriginalDialog.Resources())
        {
            using var dialog=new OriginalDialog(resource,true);
            foreach(var text in dialog.Texts.Where(text=>text.Text.Length>0))
                Check(BwccGraphics.TextFits(text),$"{resource}: Text \"{text.Text}\" wird in {text.Size} abgeschnitten.");
            // State of the original screenshots (research/screenshots), compared below.
            if(resource=="DLG_REGELN"){dialog.Check(106,true);dialog.Check(107,true);}
            if(resource=="DLG_SPEED"){dialog.Check(102,true);dialog.Check(106,true);}
            if(resource=="DLG_SPIELER")
                for(int p=0;p<6;p++){dialog.Value(101+p*4,$"Spieler {p+1}");dialog.Check(102+p*4+(p==0?0:1),true);}
            dialog.Show(window);Application.DoEvents();
            // Resource window styles: no WS_SYSMENU anywhere; three dialogs without WS_CAPTION.
            long style=WindowChrome.GetWindowLongW(dialog.Handle,-16);
            bool captionless=resource is "DLG_NEU_FRAGE" or "DLG_SPIELERAENDERUNG" or "DLG_KEINUMSCHALTEN";
            Check((style&0x80000)==0&&((style&0xC00000)==0xC00000)==!captionless&&dialog.HasCaption==!captionless,
                $"{resource}: Fensterstil {style:X} passt nicht zur Ressource (Titelleiste {!captionless}, kein Systemmenü).");
            // The screenshot shows the focus frame on the first control.
            BwccGraphics.ShowFocusForTest=resource=="DLG_REGELN"?dialog.Field(101):null;
            using var picture=new Bitmap(dialog.Width,dialog.Height);
            dialog.DrawToBitmap(picture,new Rectangle(Point.Empty,dialog.Size));
            BwccGraphics.ShowFocusForTest=null;
            picture.Save(TestOutput.PathFor($"dialog-{resource}.png"));
            if(resource=="DLG_REGELN")
            {
                // Original screenshot (research/screenshots/original-rules.png): every text line has the
                // same ink height and width, i.e. the same System-font glyphs.
                using var stream=typeof(DialogInteractionTest).Assembly.GetManifestResourceStream("Aerger.Reference.Screenshots.original-rules.png")!;
                using var original=new Bitmap(stream);
                var expected=TextRows(original,30);
                var actual=TextRows(picture,dialog.Height-dialog.ClientSize.Height);
                Check(expected.SequenceEqual(actual),$"DLG_REGELN: Textzeilen {string.Join(",",actual)}, im Original {string.Join(",",expected)}.");
            }
            if(Screenshots.TryGetValue(resource,out var screenshot))
            {
                // Client area against the original screenshot (otvdm frame: 3 px border). Left out: the
                // focused, selected first name in DLG_SPIELER (the test dialog is not active).
                using var stream=typeof(DialogInteractionTest).Assembly.GetManifestResourceStream($"Aerger.Reference.Screenshots.{screenshot.Name}.png")!;
                using var original=new Bitmap(stream);
                var client=dialog.RectangleToScreen(dialog.ClientRectangle);
                int nativeX=client.X-dialog.Left,nativeY=client.Y-dialog.Top,width=dialog.ClientSize.Width,height=dialog.ClientSize.Height;
                int originalX=3,originalY=original.Height-height-3,differing=0;
                var skipped=resource=="DLG_SPIELER"?Rectangle.Inflate(dialog.Field(101).Bounds,1,1):Rectangle.Empty;
                Check(original.Width==width+6,$"{resource}: Screenshot passt nicht zur Clientbreite {width}.");
                for(int y=0;y<height;y++)
                    for(int x=0;x<width;x++)
                        if(!skipped.Contains(x,y)&&Math.Abs(original.GetPixel(originalX+x,originalY+y).R-picture.GetPixel(nativeX+x,nativeY+y).R)>40)differing++;
                screenshotReport.Append($"{resource} {differing}/{width*height}; ");
                Check(differing<=screenshot.Limit,$"{resource}: {differing} Pixel weichen vom Original-Screenshot ab (erlaubt {screenshot.Limit}).");
            }
            dialog.Close();
        }
        Check(!window.TestBusy&&!window.TestModal&&ReferenceEquals(initialState,window.TestState)&&
            OriginalSave.Write(window.TestState).SequenceEqual(initialBytes),
            "Die Steuerungstests verändern den Spielstand oder hinterlassen eine Animation.");

        // Returns whether DLG_SPIELERAENDERUNG followed the dialog (changes during a game).
        bool RunModal(Action open,Action<OriginalDialog> interact)
        {
            bool handled=false,notice=false;
            string beforeState=window.TestInputState;
            Check(!window.TestBusy,$"Modaler Test beginnt während einer Animation: {beforeState}.");
            Exception? failure=null;
            using var timer=new System.Windows.Forms.Timer {Interval=10};
            timer.Tick+=(_,_)=>
            {
                var dialog=window.OwnedForms.OfType<OriginalDialog>().LastOrDefault(form=>form.Visible);
                if(dialog is null)return;
                if(handled)
                {
                    if(dialog.Resource=="DLG_SPIELERAENDERUNG"){notice=true;dialog.DialogResult=DialogResult.OK;}
                    else {failure??=new Exception($"Unerwarteter Folgedialog {dialog.Resource}.");dialog.DialogResult=DialogResult.Cancel;}
                    return;
                }
                handled=true;
                try
                {
                    interact(dialog);
                    Check(dialog.DialogResult!=DialogResult.None,"Dialog reagiert nicht auf Bestätigen/Abbrechen.");
                }
                catch(Exception ex) {failure=ex;dialog.DialogResult=DialogResult.Cancel;}
            };
            timer.Start();open();timer.Stop();
            if(failure is not null)throw new Exception("Dialogeingabe fehlgeschlagen.",failure);
            Check(handled&&!window.TestModal,$"Modaler Dialog wurde nicht korrekt freigegeben: handled={handled}, vorher={beforeState}, nachher={window.TestInputState}.");
            return notice;
        }
        void Command(int id)=>SendMessageW(window.Handle,0x111,(nuint)id,0);
        var pending=new Game();pending.SetPosition(0,0,5);pending.Roll(2);window.LoadState(pending);
        byte[] before=OriginalSave.Write(pending);
        void Unchanged()=>Check(!window.IsDisposed&&ReferenceEquals(window.TestState,pending)&&
            OriginalSave.Write(window.TestState).SequenceEqual(before),"Abbrechen verändert oder beendet die laufende Partie.");
        void No(OriginalDialog dialog)
        {
            var button=(BwccButton)dialog.Field(7);
            Check(button.Sample==7&&button.AccessibleName=="Nein","Nein-Schalter fehlt oder ist nicht beschriftet.");
            var question=dialog.Field(102);
            Check(BwccGraphics.TextFits(question),
                "Die Rückfrage wird abgeschnitten.");
            using var picture=new Bitmap(dialog.Width,dialog.Height);
            dialog.DrawToBitmap(picture,new Rectangle(Point.Empty,dialog.Size));
            picture.Save(TestOutput.PathFor("confirmation-test.png"));
            button.PerformClick();
            Check(dialog.DialogResult==DialogResult.No,"Nein wird als Zustimmung ausgewertet.");
        }
        RunModal(()=>Command(201),No);Unchanged();
        RunModal(()=>Command(202),No);Unchanged();
        RunModal(window.Close,No);Unchanged();
        RunModal(()=>Command(201),dialog=>Key(dialog.Field(1),Keys.Escape));Unchanged();
        RunModal(()=>Command(201),dialog=>
        {
            dialog.ActiveControl=dialog.Field(7);
            Key(dialog.Field(7),Keys.Space);
            Check(dialog.DialogResult==DialogResult.No,"Leertaste auf Nein bestätigt die Rückfrage.");
        });
        Unchanged();
        RunModal(()=>Command(206),dialog=>
        {
            dialog.Check(101,true);
            Key(dialog.Field(101),Keys.Escape);
            Check(dialog.DialogResult==DialogResult.Cancel,"Escape bricht den Regeldialog ohne Abbrechen-Knopf nicht ab.");
        });
        Unchanged();
        RunModal(()=>Command(204),dialog=>
        {
            dialog.Value(101,"Nicht übernehmen");dialog.Check(103,true);
            Key(dialog.Field(101),Keys.Escape);
        });
        Unchanged();
        bool notice=RunModal(()=>Command(204),dialog=>
        {
            dialog.Value(101,"Tastaturspieler");
            Key(dialog.Field(101),Keys.Enter);
        });
        // seg08:0522..0553: während der Partie vorgemerkt, Meldung DLG_SPIELERAENDERUNG.
        Check(notice&&window.TestState.PendingNames?[0]=="Tastaturspieler"&&window.TestState.Names[0]!="Tastaturspieler"&&
              window.TestState.AwaitingMove&&OriginalSave.Write(window.TestState).SequenceEqual(before),
            "Enter merkt die Spielereinstellungen nicht vor oder verändert den ausstehenden Zug.");
        RunModal(()=>Command(201),dialog=>Key(dialog.Field(1),Keys.Enter));
        // seg04:15ae: das Spielende übernimmt die vorgemerkte Änderung für die neue Partie.
        Check(!window.TestState.AwaitingMove&&window.TestState.Position(0,0)==-1&&window.TestState.Names[0]=="Tastaturspieler",
            "Bestätigter Neustart wird nicht ausgeführt oder übernimmt vorgemerkte Spieler nicht.");
        window.LoadState(new Game());
        File.WriteAllText(TestOutput.PathFor("dialog-test.txt"),
            "PASS: idle board during modeless control checks, background Space/Enter do not start animations or change saved state.\n"+
            "PASS: radio arrows, group wrap, disabled choices, Tab/Shift+Tab, Space, exclusive selection, independent player groups.\n"+
            "PASS: all original static texts fit their resource bounds; player edits during a game are deferred and applied by a confirmed restart (seg04:15ae).\n"+
            "PASS: checkbox Space toggle, visible/accessibly named No button, reject restart/end/close, Escape, player cancel/Enter, confirmed restart.\n"+
            "PASS: System raster font, resource window styles (no close box, three captionless dialogs), BorShade styles; client areas against original screenshots: "+screenshotReport+"\n");
    }
}
