using System.Runtime.InteropServices;

namespace Aerger;

internal static class InteractionTest
{
    // Allowed difference per event interval against the emulated original (1 ms clock):
    // WinForms timers and Task.Delay run on the ~15.6 ms system timer.
    private const int TimelineTolerance=40;
    private const double TimelinePhaseTolerance=8;
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint window,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll")] private static extern nint GetMenu(nint window);
    [DllImport("user32.dll")] private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    // Inline WinHelp jumps: original count, position, colour, mouse and Tab/Enter navigation.
    private static void CheckHotspots(HelpWindow help)
    {
        var article=help.Article;
        var green=Color.FromArgb(0,128,0).ToArgb();
        if(help.Topics.Sum(topic=>topic.Hotspots.Length)!=49||help.Topics.Any(topic=>topic.Hotspots.Any(hotspot=>
               hotspot.Length==0||hotspot.Start+hotspot.Length>topic.Text.Length||
               topic.Text.Substring(hotspot.Start,hotspot.Length).Trim().Length==0||
               !topic.Links.Contains(hotspot.Target)||!help.Topics.Any(target=>target.Id==hotspot.Target))))
            throw new Exception("Hilfe-Hotspots fehlen oder zeigen auf ungültige Textstellen.");
        if(help.Topics.Any(topic=>topic.Text.Contains('')||topic.Text.Contains('')))
            throw new Exception("Hilfetext enthält interne Hotspot-Markierungen.");
        var contents=help.CurrentTopic!;
        var first=contents.Hotspots[0];
        if(contents.Text.Substring(first.Start,first.Length)!="Copyright & Haftung"||first.Target!="Copyright")
            throw new Exception("Erster Hotspot des Inhalts ist falsch zugeordnet.");
        foreach(var hotspot in contents.Hotspots)
        {
            article.Select(hotspot.Start,hotspot.Length);
            if(article.SelectedText!=contents.Text.Substring(hotspot.Start,hotspot.Length)||
               article.SelectionColor.ToArgb()!=green||article.SelectionFont?.Underline!=true)
                throw new Exception($"Hotspot {hotspot.Target} ist nicht grün unterstrichen an seiner Textstelle.");
        }
        article.Select(0,6);
        if(article.SelectionColor.ToArgb()==green||article.SelectionFont?.Underline==true)
            throw new Exception("Normaler Hilfetext ist als Hotspot formatiert.");
        article.Select(0,0);

        Point Center(HelpWindow.Hotspot hotspot)
        {
            var point=article.GetPositionFromCharIndex(hotspot.Start+hotspot.Length/2);
            point.Offset(2,6);
            return point;
        }
        void Click(Point point)
        {
            help.BringToFront();help.Activate();article.Focus();Application.DoEvents();
            nint visibleWindow=WindowFromPoint(article.PointToScreen(point));
            if(visibleWindow!=article.Handle)
                throw new Exception($"Hilfetext am Klickpunkt verdeckt: Fenster={visibleWindow:X}, erwartet={article.Handle:X}.");
            // As with the posted key tests, do not inherit a user's held
            // Shift/Ctrl key: RichEdit would extend the selection instead.
            if(!SetKeyboardState(new byte[256]))throw new Exception("Test-Tastaturzustand konnte nicht gesetzt werden.");
            nint position=(point.Y<<16)|(point.X&0xffff);
            SendMessageW(article.Handle,0x201,1,position);
            SendMessageW(article.Handle,0x202,0,position);
            Application.DoEvents();
        }
        var beside=new Point(article.ClientSize.Width-20,Center(first).Y);
        if(help.HotspotAt(Center(first))!=first||help.HotspotAt(beside) is not null)
            throw new Exception($"Hotspot-Trefferprüfung weicht von der Textstelle ab: Punkt={Center(first)}, Index={article.GetCharIndexFromPosition(Center(first))}, erwartet={first}, Treffer={help.HotspotAt(Center(first))}, daneben={help.HotspotAt(beside)}.");
        Click(beside);
        if(help.CurrentTopic!=contents)throw new Exception("Klick neben einem Hotspot öffnet ein Thema.");
        var rules=contents.Hotspots.First(hotspot=>hotspot.Target=="Spielregeln");
        Click(Center(rules));
        if(help.CurrentTopic?.Id!="Spielregeln")throw new Exception($"Mausklick auf Hotspot öffnet das Ziel nicht: Punkt={Center(rules)}, Treffer={help.HotspotAt(Center(rules))}, Auswahl={article.SelectionStart}+{article.SelectionLength}, Thema={help.CurrentTopic?.Id}, FensterAmPunkt={WindowFromPoint(article.PointToScreen(Center(rules))):X}, HilfetextFenster={article.Handle:X}.");
        help.GoBack();

        void Key(Keys key,bool shift=false)
        {
            DialogInteractionTest.Key(article,shift?key|Keys.Shift:key);
        }
        article.Focus();article.Select(0,0);
        if(!article.Focused)throw new Exception("Hilfetext erhält keinen Tastaturfokus.");
        Key(Keys.Tab);
        if(help.SelectedHotspot!=contents.Hotspots[0])throw new Exception("Tab wählt nicht den ersten Hotspot.");
        Key(Keys.Tab);
        if(help.SelectedHotspot!=contents.Hotspots[1])throw new Exception("Tab wählt nicht den nächsten Hotspot.");
        Key(Keys.Tab,true);
        if(help.SelectedHotspot!=contents.Hotspots[0])throw new Exception("Umschalt+Tab wählt nicht den vorherigen Hotspot.");
        Key(Keys.Enter);
        if(help.CurrentTopic?.Id!=first.Target)throw new Exception("Enter folgt dem gewählten Hotspot nicht.");
        help.GoBack();
        article.Focus();
        var last=contents.Hotspots[^1];
        article.Select(last.Start,last.Length);
        Key(Keys.Tab);
        if(article.Focused||help.CurrentTopic!=contents)throw new Exception("Tab nach dem letzten Hotspot verlässt den Hilfetext nicht.");
        article.Select(0,0);
    }

    internal static void RunHelp(GameWindow window)
    {
        void CheckHelp(Action open)
        {
            bool handled=false;
            Exception? failure=null;
            byte[] before=OriginalSave.Write(window.TestState);
            using var helpTimer=new System.Windows.Forms.Timer {Interval=10};
            helpTimer.Tick+=(_,_)=>
            {
                var help=window.OwnedForms.OfType<HelpWindow>().FirstOrDefault();
                if(help is null)return;
                helpTimer.Stop();handled=true;
                try
                {
                    if(!window.TestModal||help.CurrentTopic?.Id!="Inhalt"||help.Topics.Count<30)
                        throw new Exception("Hilfe öffnet nicht mit vollständigem Inhaltsverzeichnis.");
                    if(help.Topics.Select(topic=>topic.Id).Distinct().Count()!=help.Topics.Count||
                        help.Topics.Any(topic=>topic.Links.Any(id=>!help.Topics.Any(target=>target.Id==id))))
                        throw new Exception("Hilfethemen enthalten doppelte Kennungen oder ungültige Verweise.");
                    help.OpenTopic("Spielregeln");
                    if(!help.ArticleText.Contains("würfeln",StringComparison.CurrentCultureIgnoreCase)||
                        help.ArticleText.Contains("XL.UV")||help.ArticleText.Contains("Inhalt"))
                        throw new Exception("Hilfethema zeigt falschen Inhalt oder interne WinHelp-Kennungen.");
                    help.OpenTopic("4J90_OD");
                    if(help.CurrentTopic?.Title!="Fenstergröße")throw new Exception("Umlaute im Hilfetitel fehlen.");
                    help.GoBack();
                    if(help.CurrentTopic?.Id!="Spielregeln")throw new Exception("Hilfeverlauf verliert das vorherige Thema.");
                    help.Filter("Startfeld");
                    if(help.ResultCount==0||help.ResultCount>=help.Topics.Count)
                        throw new Exception("Volltextsuche filtert die Hilfethemen nicht.");
                    help.Filter("unauffindbares-hilfethema-xyz");
                    if(help.ResultCount!=0||help.CurrentTopic?.Id!="Spielregeln")
                        throw new Exception("Leere Suchergebnisse verändern das geöffnete Hilfethema.");
                    help.Filter("");help.GoBack();
                    if(help.ResultCount!=help.Topics.Count||help.CurrentTopic?.Id!="Inhalt")
                        throw new Exception("Inhaltsverzeichnis oder Verlauf wird nicht wiederhergestellt.");
                    CheckHotspots(help);
                    using var picture=new Bitmap(help.Width,help.Height);
                    help.DrawToBitmap(picture,new Rectangle(Point.Empty,help.Size));
                    picture.Save(TestOutput.PathFor("help-test.png"));
                }
                catch(Exception ex) {failure=ex;}
                finally {help.Close();}
            };
            helpTimer.Start();open();
            if(failure is not null)throw new Exception("Hilfeprüfung fehlgeschlagen.",failure);
            if(!handled||window.TestModal||!OriginalSave.Write(window.TestState).SequenceEqual(before))
                throw new Exception("Hilfeöffnung verändert den Spielstand oder blockiert die Zugsteuerung.");
        }
        CheckHelp(()=>SendMessageW(window.Handle,0x111,901,0));
        CheckHelp(()=>DialogInteractionTest.Key(window,Keys.F1));
        File.WriteAllText(TestOutput.PathFor("help-test.txt"),
            "PASS: help menu/F1, topics, search, history, inline mouse/keyboard hotspots, unchanged saved state and modal restoration.\n");
    }
    // research/emulate_timeline.py references (--timeline-test): computer-only games with the
    // original RandSeed, compared event by event with AERGER.EXE.
    internal static void RunTimelines(GameWindow window)
    {
        // Same RandSeed, same speed: identical dice faces, moves, sounds and order of events,
        // and every interval within the timer tolerance.
        var timelineReport=new System.Text.StringBuilder();
        window.PrepareInteractionTest();
        foreach(var resource in typeof(InteractionTest).Assembly.GetManifestResourceNames().Where(name=>name.Contains(".Timeline.")).Order())
        {
            string[] reference;
            using(var reader=new StreamReader(typeof(InteractionTest).Assembly.GetManifestResourceStream(resource)!))
                reference=reader.ReadToEnd().Replace("\r","").Split('\n',StringSplitOptions.RemoveEmptyEntries);
            var header=System.Text.RegularExpressions.Regex.Match(reference[0],@"seed 0x([0-9a-f]+), speed (\d), (\d) computer players, (\d+) rolls, rules ([01]{7})(, sounds on)?");
            uint seed=Convert.ToUInt32(header.Groups[1].Value,16);
            int speed=int.Parse(header.Groups[2].Value),players=int.Parse(header.Groups[3].Value),rolls=int.Parse(header.Groups[4].Value);
            var expected=reference.Skip(1).Select(line=>(Time:long.Parse(line[..line.IndexOf(' ')]),Text:line[(line.IndexOf(' ')+1)..])).ToArray();
            var flags=header.Groups[5].Value.Select(flag=>flag=='1').ToArray();
            // A computer game never opens a dialog here; one that does would block the run.
            var unexpectedDialogs=new List<string>();
            using var dialogWatch=new System.Windows.Forms.Timer {Interval=50};
            dialogWatch.Tick+=(_,_)=>
            {
                foreach(var dialog in window.OwnedForms.OfType<OriginalDialog>().Where(form=>form.Visible))
                {unexpectedDialogs.Add(dialog.Resource);dialog.DialogResult=DialogResult.Cancel;}
            };
            dialogWatch.Start();
            window.TestRollLimit=rolls;
            window.TestStartComputerGame(seed,speed,players,new Rules(flags[0],flags[1],flags[2],flags[3],flags[4],flags[5],flags[6]),header.Groups[6].Success);
            var log=window.TestTimeline!;
            long deadline=Environment.TickCount64+expected[^1].Time+20000;
            while(log.Count(line=>line.Contains(" rolled "))<rolls&&Environment.TickCount64<deadline){Application.DoEvents();Thread.Sleep(1);}
            dialogWatch.Stop();
            File.WriteAllLines(TestOutput.PathFor(resource.Replace("Aerger.Reference.Timeline.","native-")),log);
            if(unexpectedDialogs.Count>0)
                throw new Exception($"{resource}: unerwarteter Dialog {string.Join(",",unexpectedDialogs)}; zuletzt: {string.Join(" | ",log.TakeLast(5))}");
            window.TestStopGame();
            long stopDeadline=Environment.TickCount64+10000;
            while(window.TestBusy&&Environment.TickCount64<stopDeadline){Application.DoEvents();Thread.Sleep(1);}
            if(window.TestBusy||window.TestModal)
                throw new Exception($"{resource}: Partie hält nicht an ({window.TestInputState}); zuletzt: {string.Join(" | ",log.TakeLast(5))}");
            var actual=log.Select(line=>(Time:long.Parse(line[..line.IndexOf(' ')]),Text:line[(line.IndexOf(' ')+1)..])).Take(expected.Length).ToArray();
            window.TestTimeline=null;
            File.WriteAllLines(TestOutput.PathFor(resource.Replace("Aerger.Reference.Timeline.","native-")),actual.Select(e=>$"{e.Time} {e.Text}"));
            if(actual.Length<expected.Length)
                throw new Exception($"{resource}: nur {actual.Length} von {expected.Length} Ereignissen.");
            long worst=0;
            for(int i=0;i<expected.Length;i++)
            {
                if(actual[i].Text!=expected[i].Text)
                    throw new Exception($"{resource}: Ereignis {i} ist \"{actual[i].Text}\", das Original hat \"{expected[i].Text}\".");
                long original=expected[i].Time-(i==0?0:expected[i-1].Time),native=actual[i].Time-(i==0?0:actual[i-1].Time);
                worst=Math.Max(worst,Math.Abs(native-original));
                if(Math.Abs(native-original)>TimelineTolerance)
                    throw new Exception($"{resource}: Abstand vor Ereignis {i} ({expected[i].Text}) {native} ms, im Original {original} ms.");
                // No accumulated drift either. At speed 3 the original's pace is one idle-loop pass
                // per frame, i.e. machine-dependent (1 ms in the emulation), so only intervals count.
                long drift=actual[i].Time-expected[i].Time;
                if(speed<3&&Math.Abs(drift)>100+expected[i].Time/100)
                    throw new Exception($"{resource}: bei Ereignis {i} ({expected[i].Text}) {drift} ms gegenüber dem Original verschoben.");
            }
            // Per transition kind (e.g. "moved->roll" = T0, "face->face" = frame delay) the mean
            // deviation must stay small, so a wrong delay constant cannot hide in the tolerance.
            var phases=Enumerable.Range(1,expected.Length-1)
                .GroupBy(i=>expected[i-1].Text.Split(' ')[0]+"->"+expected[i].Text.Split(' ')[0])
                .Select(group=>(Kind:group.Key,Count:group.Count(),Mean:group.Average(i=>(double)(actual[i].Time-actual[i-1].Time)-(expected[i].Time-expected[i-1].Time))))
                .ToArray();
            // A single WM_TIMER wait can overshoot by ~15 ms; the bound tightens with more samples.
            var phase=phases.MaxBy(p=>Math.Abs(p.Mean)-24.0/p.Count);
            if(speed<3&&Math.Abs(phase.Mean)>TimelinePhaseTolerance+24.0/phase.Count)
                throw new Exception($"{resource}: Übergang {phase.Kind} weicht im Mittel {phase.Mean:F1} ms vom Original ab ({phase.Count} Fälle).");
            timelineReport.Append($"{resource}: {expected.Length} events, max interval deviation {worst} ms, closest mean phase {phase.Kind} {phase.Mean:F1} ms over {phase.Count}; ");
        }
        File.WriteAllText(TestOutput.PathFor("timeline-test.txt"),
            "PASS: dice, sounds and timing match AERGER.EXE timelines (same RandSeed, all speeds, both boards, sounds off and on with DS:173e sample durations): "+timelineReport+"\n");
    }
    internal static void Run(GameWindow window)
    {
        if((WindowChrome.GetWindowLongW(window.Handle,-20)&8)!=0)throw new Exception("Fenster darf nicht Topmost sein.");
        if(GetMenuItemCount(GetMenu(window.Handle))!=4)throw new Exception("Original-Menü fehlt.");
        window.PrepareInteractionTest();
        if(window.TestState.Player!=0)throw new Exception("Neues Spiel startet nicht mit Spieler 1.");
        DialogInteractionTest.Run(window);
        RunHelp(window);
        void Key(Keys key)
        {
            int before=window.TestKeyCount;
            // Dispatch with the explicit modifier state before pumping other
            // messages; queued physical input can otherwise add Alt/Shift.
            DialogInteractionTest.Key(window,key);
            if(window.TestKeyCount==before||window.TestLastKey!=key)
                throw new Exception($"Tastennachricht {key} nicht verarbeitet (zuletzt {window.TestLastKey}, {window.TestInputState}).");
            var deadline = Environment.TickCount64 + 10000;
            while (window.TestBusy && Environment.TickCount64 < deadline)
            { Application.DoEvents(); Thread.Sleep(5); }
            if (window.TestBusy) throw new Exception("Animation wird nicht beendet.");
        }
        // Tab until the given piece is shown; the first Tab starts at the remembered piece (DS:1d88).
        void Select(int piece)
        {
            for(int press=0;press<5;press++){Key(Keys.Tab);if(window.TestSelectionIndex==piece)return;}
            throw new Exception($"Figur {piece+1} lässt sich mit Tab nicht wählen.");
        }
        bool noticeCaptured=false;
        // Returns whether DLG_SPIELERAENDERUNG ("nach dem nächsten Zug wirksam") followed the dialog.
        bool Dialog(int command,Action<OriginalDialog> apply,DialogResult result=DialogResult.OK)
        {
            bool handled=false,notice=false;
            Exception? failure=null;
            using var dialogTimer=new System.Windows.Forms.Timer {Interval=10};
            dialogTimer.Tick+=(_,_)=>
            {
                var dialog=window.OwnedForms.OfType<OriginalDialog>().LastOrDefault(form=>form.Visible);
                if(dialog is null)return;
                if(handled)
                {
                    if(dialog.Resource!="DLG_SPIELERAENDERUNG")
                    {failure??=new Exception($"Unerwarteter Folgedialog {dialog.Resource}.");dialog.DialogResult=DialogResult.Cancel;return;}
                    if(!noticeCaptured){noticeCaptured=true;using var picture=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(picture,new Rectangle(Point.Empty,dialog.Size));picture.Save(TestOutput.PathFor("change-notice-test.png"));}
                    notice=true;dialog.DialogResult=DialogResult.OK;return;
                }
                handled=true;
                try {apply(dialog);}
                catch(Exception ex) {failure=ex;}
                dialog.DialogResult=failure is null?result:DialogResult.Cancel;
            };
            dialogTimer.Start();
            SendMessageW(window.Handle,0x111,(nuint)command,0);
            dialogTimer.Stop();
            if(failure is not null)throw new Exception("Dialogprüfung fehlgeschlagen.",failure);
            if(!handled||window.TestModal)throw new Exception("Menüdialog wurde nicht geöffnet oder korrekt freigegeben.");
            return notice;
        }
        var pendingSizing=new Game();pendingSizing.Roll(6);window.LoadState(pendingSizing);
        byte[] beforeSizing=OriginalSave.Write(window.TestState);
        void CheckClient(Size expected)
        {
            WindowChrome.GetClientRect(window.Handle,out var actual);
            if(window.ClientSize!=expected||actual.Right!=expected.Width||actual.Bottom!=expected.Height)
                throw new Exception($"Falsche Fenster-Innenmaße: {window.ClientSize}, erwartet {expected}.");
        }
        foreach(int board in new[]{200,400,700})
        {
            int index=board==200?0:board==400?1:2;
            Dialog(303,dialog=>{for(int option=0;option<3;option++)dialog.Check(101+option,option==index);});
            CheckClient(WindowSizing.ClientForBoard(board));
        }
        // Exercise the native enter/resize/exit path rather than invoking the sizing helper.
        foreach(var requested in new[]{new Size(600,500),new Size(800,600),new Size(400,300),new Size(860,704)})
        {
            SendMessageW(window.Handle,0x231,0,0);
            var outer=WindowChrome.OuterForClient(window.Handle,requested);
            WindowChrome.SetWindowPos(window.Handle,0,0,0,outer.Width,outer.Height,0x16);
            SendMessageW(window.Handle,0x232,0,0);
            CheckClient(WindowSizing.ClientForBoard(WindowSizing.LargestFittingBoard(requested)));
        }
        var work=Screen.FromHandle(window.Handle).WorkingArea;
        var frame=WindowChrome.OuterForClient(window.Handle,Size.Empty);
        var largest=WindowSizing.ClientForBoard(WindowSizing.LargestFittingBoard(new(work.Width-frame.Width,work.Height-frame.Height)));
        SendMessageW(window.Handle,0x112,0xf030,0);
        CheckClient(largest);
        if(window.WindowState!=FormWindowState.Normal||!work.Contains(window.Bounds))
            throw new Exception("Maximieren verlässt die festen Größen oder den Bildschirmarbeitsbereich.");
        window.WindowState=FormWindowState.Minimized;
        window.WindowState=FormWindowState.Normal;
        Application.DoEvents();
        CheckClient(largest);
        window.WindowState=FormWindowState.Maximized;
        Application.DoEvents();
        CheckClient(largest);
        if(window.WindowState!=FormWindowState.Normal)
            throw new Exception("Direktes Maximieren umgeht die festen Brettgrößen.");
        if(!OriginalSave.Write(window.TestState).SequenceEqual(beforeSizing)||window.TestModal)
            throw new Exception("Fenstergrößenänderung verändert den Spielstand oder blockiert das Spiel.");
        for(int restart=0;restart<3;restart++)
        {
            Dialog(201,_=>{});
            if(window.TestState.Player!=0)throw new Exception("Neustart wählt einen zufälligen Startspieler.");
        }
        if(!Dialog(204,dialog=>{dialog.Check(102,false);dialog.Check(104,true);})||window.TestState.Kinds[0]!=PlayerKind.Human)
            throw new Exception("Spieleränderung während der Partie wird nicht wie im Original vorgemerkt.");
        Dialog(201,_=>{});
        if(window.TestState.Player!=1)throw new Exception("Bei inaktivem Spieler 1 beginnt nicht der erste aktive Spieler.");
        Dialog(204,dialog=>{dialog.Check(104,false);dialog.Check(102,true);});
        Dialog(201,_=>{});
        if(window.TestState.Player!=0)throw new Exception("Reaktivierter Spieler 1 beginnt das nächste neue Spiel nicht.");
        Dialog(301,dialog=>{dialog.Check(101,false);dialog.Check(102,true);});
        Dialog(302,dialog=>
        {
            for(int speed=0;speed<4;speed++)
            {dialog.Check(101+speed,speed==1);dialog.Check(105+speed,speed==1);}
        });
        var samples=new List<int>();
        window.TestSound.SamplePlayed+=samples.Add;
        window.TestNextDie=6;
        Key(Keys.Space);
        if(!window.TestState.AwaitingMove)throw new Exception("Würfeln per Tastatur funktioniert nicht.");
        int player=window.TestState.Player;
        Key(Keys.Tab);Key(Keys.Enter);
        if(window.TestState.AwaitingMove||!Enumerable.Range(0,4).Any(i=>window.TestState.Position(player,i)>=0))
            throw new Exception("Figurenauswahl und Ziehen per Tastatur funktionieren nicht.");
        if(!samples.Contains(5)||samples.Contains(4))throw new Exception("Hofauszug spielt einen zusätzlichen Abschlusston.");
        Game PendingMove(bool capture)
        {
            var state=new Game();state.SetPosition(0,0,5);
            if(capture)state.SetPosition(1,0,37);
            state.Roll(2);return state;
        }
        window.LoadState(PendingMove(false));samples.Clear();Key(Keys.Tab);Key(Keys.Enter);
        if(!samples.SequenceEqual(new[]{6,6}))
            throw new Exception("Normaler Zug spielt den Schlag-/Abschlusston oder verliert seine Schrittgeräusche.");
        for(int speed=0;speed<4;speed++)
        {
            Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==speed);});
            var capture=PendingMove(true);
            var lastSound=new System.Diagnostics.Stopwatch();
            int lastSoundDuration=0;
            void ObserveLastSound(int sample)
            {lastSoundDuration=window.TestSound.RemainingMilliseconds;lastSound.Restart();}
            window.TestSound.SamplePlayed+=ObserveLastSound;
            try {window.LoadState(capture);samples.Clear();Key(Keys.Tab);Key(Keys.Enter);}
            finally {window.TestSound.SamplePlayed-=ObserveLastSound;}
            int[] expected=speed<2?[3,6,6,4]:speed==2?[3,6,6]:[];
            if(capture.Position(1,0)!=-1||!samples.SequenceEqual(expected))
                throw new Exception($"Schlaggeräusche bei Tempo {speed}: [{string.Join(',',samples)}], erwartet [{string.Join(',',expected)}]; Opferposition {capture.Position(1,0)}.");
            if(speed<3&&lastSound.ElapsedMilliseconds<lastSoundDuration-20)
                throw new Exception($"Schlagzug Tempo {speed} beendet den Zug vor dem Ende des letzten Tons.");
            var yardCapture=new Game();yardCapture.SetPosition(1,0,30);yardCapture.Roll(6);
            window.LoadState(yardCapture);samples.Clear();Key(Keys.Tab);Key(Keys.Enter);
            if(yardCapture.Position(0,0)!=0||yardCapture.Position(1,0)!=-1||
                !samples.SequenceEqual(speed<2?new[]{3,4}:Array.Empty<int>()))
                throw new Exception($"Hof-Schlagzug Tempo {speed}: [{string.Join(',',samples)}]; Positionen Angreifer={yardCapture.Position(0,0)}, Opfer={yardCapture.Position(1,0)}, Auswahlphase={yardCapture.AwaitingMove}.");
            var goal=new Game();goal.SetPosition(0,0,39);goal.Roll(2);
            window.LoadState(goal);samples.Clear();Key(Keys.Tab);Key(Keys.Enter);
            if(goal.Position(0,0)!=41||!samples.SequenceEqual(speed<3?new[]{6,6}:Array.Empty<int>()))
                throw new Exception("Zieleinlauf verliert Schritte oder löst Schlaggeräusche aus.");
        }
        // Dice sound lasts longer than the speed-1 animation. Speech must not
        // replace it prematurely (seg04:0673), and fast modes still wait when
        // speech is enabled even though they suppress the spoken number.
        Dialog(301,dialog=>dialog.Check(101,true));
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==1);});
        var diceSound=new System.Diagnostics.Stopwatch();
        int diceDuration=0;
        long speechStarted=-1;
        void ObserveRoll(int sample)
        {
            if(sample==2){diceDuration=window.TestSound.RemainingMilliseconds;diceSound.Restart();}
            if(sample==14)speechStarted=diceSound.ElapsedMilliseconds;
        }
        window.TestSound.SamplePlayed+=ObserveRoll;
        try
        {
            window.LoadState(new Game());window.TestNextDie=6;samples.Clear();Key(Keys.Space);
            if(!samples.SequenceEqual(new[]{2,14})||speechStarted<diceDuration-20)
                throw new Exception("Würfelansage unterbricht das Würfelgeräusch.");
        }
        finally {window.TestSound.SamplePlayed-=ObserveRoll;}
        foreach(int speed in new[]{2,3})
        {
            Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==speed);});
            window.LoadState(new Game());window.TestNextDie=6;
            int duration=window.TestSound.Play(9,false);
            var wait=System.Diagnostics.Stopwatch.StartNew();
            samples.Clear();Key(Keys.Space);
            if(samples.Count!=0||wait.ElapsedMilliseconds<duration-20)
                throw new Exception($"Schnelles Würfeln Tempo {speed} überspringt die Tonwartezeit oder spielt eine Ansage.");
        }
        Dialog(301,dialog=>dialog.Check(101,false));
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==1);});
        Dialog(303,dialog=>{dialog.Check(101,false);dialog.Check(102,false);dialog.Check(103,true);});
        // Compare the victim's track/yard regions with a rendering without that
        // player, including during the return sound before the turn completes.
        var backgroundState=PendingMove(true);backgroundState.Kinds[1]=PlayerKind.Inactive;
        window.LoadState(backgroundState);
        using(var background=window.TestBoardImage())
        using(var victim=new Archive(Path.Combine(AppContext.BaseDirectory,"Data"),"GRAPH03").Bitmap(42))
        using(var victimInYard=new Archive(Path.Combine(AppContext.BaseDirectory,"Data"),"GRAPH03").Bitmap(43))
        using(var attacker=new Archive(Path.Combine(AppContext.BaseDirectory,"Data"),"GRAPH03").Bitmap(40))
        {
            var capture=PendingMove(true);
            Point track=BoardLayout.PieceAt(capture,2,1,0);
            Point yard=BoardLayout.PieceAt(capture,2,1,0,-1);
            var trackBounds=new Rectangle(track,victim.Size);
            var yardBounds=new Rectangle(yard,victim.Size);
            bool SameRegion(Bitmap picture,Rectangle region)
            {
                for(int y=region.Top;y<region.Bottom;y++)
                    for(int x=region.Left;x<region.Right;x++)
                        if(picture.GetPixel(x,y)!=background.GetPixel(x,y))return false;
                return true;
            }
            bool Shows(Bitmap picture,Bitmap sprite,Point origin)
            {
                for(int y=0;y<sprite.Height;y++)
                    for(int x=0;x<sprite.Width;x++)
                        if(picture.GetPixel(origin.X+x,origin.Y+y)!=sprite.GetPixel(x,y))return false;
                return true;
            }
            int duration=window.TestSound.Play(3,false);
            var watch=new System.Diagnostics.Stopwatch();
            long firstStep=-1;
            bool sadAtIntro=false,sadWhileStepping=false,hiddenAtReturn=false;
            void ObserveCapture(int sample)
            {
                // seg04:13da: with sample 3 the victim turns sad on its field and stays
                // there until the attacker arrives; its yard slot stays empty until the return.
                if(sample==3)
                {
                    watch.Restart();
                    using var frame=window.TestBoardImage();
                    sadAtIntro=Shows(frame,victimInYard,track)&&SameRegion(frame,yardBounds);
                    frame.Save(TestOutput.PathFor("capture-intro-test.png"));
                }
                if(sample==6&&firstStep<0)
                {
                    firstStep=watch.ElapsedMilliseconds;
                    using var frame=window.TestBoardImage();
                    sadWhileStepping=Shows(frame,victimInYard,track)&&SameRegion(frame,yardBounds);
                }
                if(sample==4)
                {
                    using var frame=window.TestBoardImage();
                    hiddenAtReturn=SameRegion(frame,yardBounds)&&Shows(frame,attacker,track)&&capture.Player==0&&capture.AwaitingMove;
                    frame.Save(TestOutput.PathFor("capture-return-test.png"));
                }
            }
            window.TestSound.SamplePlayed+=ObserveCapture;
            try {window.LoadState(capture);samples.Clear();Key(Keys.Tab);Key(Keys.Enter);}
            finally {window.TestSound.SamplePlayed-=ObserveCapture;}
            if(!sadAtIntro||!sadWhileStepping||!hiddenAtReturn||firstStep<duration-20)
                throw new Exception($"Schlaganimation weicht von seg04:13da ab (traurig am Feld {sadAtIntro}/{sadWhileStepping}, Rückkehr {hiddenAtReturn}) oder überlagert den Schlagton mit Schritten.");
            using var completed=window.TestBoardImage();
            CheckSprite(completed,victimInYard,yardBounds.Location);
        }
        // seg05:018a/020c: a step draws the moving piece over a piece it passes,
        // even one of a later player.
        var passing=new Game();passing.SetPosition(0,0,5);passing.SetPosition(2,0,27);passing.Roll(3);
        bool onTop=false;int steps=0;
        void ObservePassing(int sample)
        {
            if(sample!=6||++steps!=2)return;
            using var frame=window.TestBoardImage();
            using var attackerSprite=new Archive(Path.Combine(AppContext.BaseDirectory,"Data"),"GRAPH03").Bitmap(40);
            for(int y=0;y<attackerSprite.Height;y++)
                for(int x=0;x<attackerSprite.Width;x++)
                    if(frame.GetPixel(BoardLayout.PieceAt(passing,2,0,0,7).X+x,BoardLayout.PieceAt(passing,2,0,0,7).Y+y)!=attackerSprite.GetPixel(x,y))return;
            onTop=true;
        }
        window.TestSound.SamplePlayed+=ObservePassing;
        try {window.LoadState(passing);Key(Keys.Tab);Key(Keys.Enter);}
        finally {window.TestSound.SamplePlayed-=ObservePassing;}
        if(!onTop||passing.Position(0,0)!=8||passing.Position(2,0)!=27)
            throw new Exception("Ziehende Figur liegt beim Überqueren nicht über der fremden Figur.");
        // seg04:13e6/0e4a: with effects on, a capture waits for the current sample even
        // when the speed gate suppresses sample 3 (speed 3: no samples, no step delay).
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==3);});
        {
            var fastCapture=PendingMove(true);window.LoadState(fastCapture);
            Key(Keys.Tab);
            int pending=window.TestSound.Play(9,false);
            var wait=System.Diagnostics.Stopwatch.StartNew();
            // Halfway through the pending sample the attacker must still stand on its field.
            bool waitedAtStart=false;
            using var midway=new System.Windows.Forms.Timer {Interval=Math.Max(1,pending/2)};
            midway.Tick+=(_,_)=>
            {
                midway.Stop();
                using var frame=window.TestBoardImage();
                using var attackerSprite=new Archive(Path.Combine(AppContext.BaseDirectory,"Data"),"GRAPH03").Bitmap(40);
                var start=BoardLayout.PieceAt(fastCapture,2,0,0,5);
                waitedAtStart=window.TestBusy;
                for(int y=0;y<attackerSprite.Height&&waitedAtStart;y++)
                    for(int x=0;x<attackerSprite.Width;x++)
                        if(frame.GetPixel(start.X+x,start.Y+y)!=attackerSprite.GetPixel(x,y)){waitedAtStart=false;break;}
            };
            samples.Clear();midway.Start();Key(Keys.Enter);
            if(samples.Count!=0||!waitedAtStart||wait.ElapsedMilliseconds<pending-20||fastCapture.Position(0,0)!=7)
                throw new Exception($"Schlag bei Tempo 3 wartet nicht vor dem Zug auf den laufenden Klang (Mitte am Start: {waitedAtStart}).");
        }
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==1);});
        // seg04:0b3e erases the die after every move (also before a bonus roll), seg04:0022
        // on every next-player transition; failed entry attempts keep it (paint: DS:2173/2175).
        {
            var dieAfterMove=new Game();dieAfterMove.SetPosition(0,0,5);dieAfterMove.Roll(6);window.LoadState(dieAfterMove);
            if(window.TestShownDie!=6)throw new Exception("Geladener offener Zug zeigt den Würfel nicht.");
            Key(Keys.Tab);Key(Keys.Enter);
            if(window.TestShownDie!=0||dieAfterMove.Player!=0)throw new Exception("Würfel bleibt nach dem Zug (vor dem Bonuswurf) stehen.");
            window.LoadState(new Game());
            if(window.TestShownDie!=0)throw new Exception("Geladener Spielstand ohne offenen Zug zeigt einen Würfel.");
            var attempts=new Game();window.LoadState(attempts);
            window.TestNextDie=3;Key(Keys.Space);
            if(window.TestShownDie!=3||attempts.Attempts!=1)throw new Exception("Würfel verschwindet nach einem Fehlversuch.");
            window.TestNextDie=4;Key(Keys.Space);window.TestNextDie=5;Key(Keys.Space);
            if(window.TestShownDie!=0||attempts.Player!=1)throw new Exception("Würfel bleibt nach dem dritten Fehlversuch beim nächsten Spieler stehen.");
        }
        // Computer speed is separate from human speed: speed 2 still plays
        // sample 3 on the track but not sample 4 when returning the victim.
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(101+option,option==2);});
        var computerCapture=PendingMove(true);computerCapture.Kinds[0]=PlayerKind.Computer;
        samples.Clear();window.LoadState(computerCapture);
        long computerDeadline=Environment.TickCount64+10000;
        while((computerCapture.AwaitingMove||window.TestBusy)&&Environment.TickCount64<computerDeadline)
        {Application.DoEvents();Thread.Sleep(5);}
        if(computerCapture.AwaitingMove||window.TestBusy||!samples.SequenceEqual(new[]{3,6,6}))
            throw new Exception("Computer-Schlagzug verwendet nicht seine eigene Geschwindigkeitsstufe.");
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(101+option,option==1);});
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==2);});
        var sixCapture=new Game(6);sixCapture.SetPosition(0,0,5);sixCapture.SetPosition(1,0,47);sixCapture.Roll(2);
        window.LoadState(sixCapture);samples.Clear();Key(Keys.Tab);Key(Keys.Enter);
        if(sixCapture.Position(1,0)!=-1||!samples.SequenceEqual(new[]{3,6,6}))
            throw new Exception("Sechs-Spieler-Schlagzug hat eine falsche Schritt-/Tonfolge.");
        var sixGoal=new Game(6);sixGoal.SetPosition(0,0,47);sixGoal.Roll(2);
        window.LoadState(sixGoal);samples.Clear();Key(Keys.Tab);Key(Keys.Enter);
        if(sixGoal.Position(0,0)!=49||!samples.SequenceEqual(new[]{6,6}))
            throw new Exception("Sechs-Spieler-Zieleinlauf verliert einen Schritt.");
        var wrap=new Game(6,1);wrap.SetPosition(1,0,38);wrap.Roll(4);
        var wrapFrames=new List<Bitmap>();
        void ObserveWrap(int sample) {if(sample==6)wrapFrames.Add(window.TestBoardImage());}
        window.TestSound.SamplePlayed+=ObserveWrap;
        try
        {
            window.LoadState(wrap);samples.Clear();Key(Keys.Tab);Key(Keys.Enter);
            if(wrap.Position(1,0)!=42||wrapFrames.Count!=4||!samples.SequenceEqual(new[]{6,6,6,6}))
                throw new Exception("Rundlauf überspringt das Ende der Sechs-Spieler-Strecke.");
            // Absolute original fields 48,1,2,3 (segmented track wraps at 48).
            var wrapTrack=BoardLayout.Track(true,2);int[] wrapFields=[47,0,1,2];
            using var sprite=new Archive(Path.Combine(AppContext.BaseDirectory,"Data"),"GRAPH03").Bitmap(50);
            for(int step=0;step<wrapFields.Length;step++)
                CheckSprite(wrapFrames[step],sprite,new(wrapTrack[wrapFields[step]].X+2,wrapTrack[wrapFields[step]].Y+2));
            wrapFrames[1].Save(TestOutput.PathFor("wrap-step-test.png"));
        }
        finally
        {
            window.TestSound.SamplePlayed-=ObserveWrap;
            foreach(var wrapFrame in wrapFrames)wrapFrame.Dispose();
        }
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==1);});
        Dialog(301,dialog=>dialog.Check(102,false));
        window.LoadState(PendingMove(true));samples.Clear();Key(Keys.Tab);Key(Keys.Enter);
        if(samples.Count!=0)throw new Exception("Deaktivierte Soundeffekte werden weiterhin abgespielt.");
        Dialog(301,dialog=>dialog.Check(102,true));
        window.TestSound.SamplePlayed-=samples.Add;
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==3);});
        foreach(int players in new[]{4,6})
            foreach(bool packed in new[]{false,true})
            {
                var entryAttempts=new Game(players);
                entryAttempts.SetPosition(0,0,entryAttempts.TrackLength+(packed?3:0));
                window.LoadState(entryAttempts);
                for(int attempt=1;attempt<=(packed?3:1);attempt++)
                {
                    window.TestNextDie=5;Key(Keys.Space);
                    int nextPlayer=packed&&attempt<3?0:1;
                    if(entryAttempts.Player!=nextPlayer||entryAttempts.AwaitingMove||
                       entryAttempts.Attempts!=(nextPlayer==0?attempt:0))
                        throw new Exception($"Startversuche per Tastatur: Brett {players}, Zielende belegt={packed}, Versuch {attempt}.");
                }
                window.TestNextDie=6;Key(Keys.Space);Key(Keys.Tab);Key(Keys.Enter);
                if(entryAttempts.Player!=1||entryAttempts.Position(1,0)!=0||entryAttempts.AwaitingMove)
                    throw new Exception("Nach Startversuchen kann der nächste Spieler nicht würfeln und ziehen.");
                var repeatEntry=new Game(players) {Rules=new Rules(RepeatOnOne:true)};
                repeatEntry.SetPosition(0,0,repeatEntry.TrackLength+3);
                window.LoadState(repeatEntry);
                for(int attempt=1;attempt<=3;attempt++)
                {
                    window.TestNextDie=1;Key(Keys.Space);
                    if(repeatEntry.Player!=(attempt==3?1:0)||repeatEntry.AwaitingMove)
                        throw new Exception("Nochmal bei eins verlängert drei erfolglose Startversuche.");
                }
            }
        Dialog(302,dialog=>{for(int option=0;option<4;option++)dialog.Check(105+option,option==1);});
        // seg08:0f8d..0fbb, seg04:0315..032d: während der Partie geänderte Regeln wirken
        // erst beim nächsten Spielerwechsel; der laufende Zug nutzt die bisherigen Regeln.
        var changedRules=new Game {Rules=new Rules(EnterOnOne:true)};
        changedRules.Roll(1);
        window.LoadState(changedRules);
        Key(Keys.Tab);
        if(!Dialog(206,dialog=>dialog.Check(106,false)))
            throw new Exception("Regeländerung während der Partie zeigt DLG_SPIELERAENDERUNG nicht.");
        if(!changedRules.AwaitingMove||changedRules.Player!=0||!changedRules.Rules.EnterOnOne||changedRules.PendingRules?.EnterOnOne!=false)
            throw new Exception("Regeländerung wirkt vor dem nächsten Spielerwechsel.");
        if(Dialog(206,dialog=>{if(dialog.Checked(106))throw new Exception("Regeldialog zeigt nicht die vorgemerkten Regeln.");},DialogResult.Cancel)||
           changedRules.PendingRules is null)
            throw new Exception("Abbrechen verwirft vorgemerkte Regeln oder meldet eine Änderung.");
        Key(Keys.Enter);
        if(changedRules.Position(0,0)!=0||changedRules.Rules.EnterOnOne||changedRules.PendingRules is not null||changedRules.Player!=1)
            throw new Exception("Vorgemerkte Regeln werden beim Spielerwechsel nicht übernommen.");
        var keptSelection=new Game();
        int[] positions=[5,7,20,30];
        for(int piece=0;piece<4;piece++)keptSelection.SetPosition(0,piece,positions[piece]);
        keptSelection.Roll(3);
        window.LoadState(keptSelection);Key(Keys.Tab);
        if(!Dialog(206,dialog=>dialog.Check(105,true)))throw new Exception("Regeländerung meldet die verzögerte Übernahme nicht.");
        Key(Keys.Enter);
        if(keptSelection.AwaitingMove||keptSelection.Position(0,0)!=8||!keptSelection.Rules.NoJump||keptSelection.Player!=1)
            throw new Exception("Figurenauswahl/Zug nutzt nicht die bis zum Spielerwechsel gültigen Regeln.");
        var canceledRules=new Game {Rules=new Rules(EnterOnOne:true)};
        canceledRules.Roll(1);window.LoadState(canceledRules);
        var beforeCancel=OriginalSave.Write(canceledRules);
        if(Dialog(206,dialog=>dialog.Check(106,false),DialogResult.Cancel)||canceledRules.HasPendingChanges||
           !OriginalSave.Write(canceledRules).SequenceEqual(beforeCancel))
            throw new Exception("Abbrechen des Regeldialogs verändert die Partie.");
        // seg08:0522..0553: Spieleränderungen werden vorgemerkt; Spieler 1 behält seinen Wurf.
        if(!Dialog(204,dialog=>{dialog.Check(102,false);dialog.Check(103,false);dialog.Check(104,true);})||
           canceledRules.Player!=0||!canceledRules.AwaitingMove||canceledRules.Kinds[0]!=PlayerKind.Human||
           !OriginalSave.Write(canceledRules).SequenceEqual(beforeCancel))
            throw new Exception("Spielerdialog ändert die laufende Partie vor dem Spielerwechsel.");
        Key(Keys.Tab);Key(Keys.Enter);
        if(canceledRules.Kinds[0]!=PlayerKind.Inactive||canceledRules.Player!=1||canceledRules.AwaitingMove||
           Enumerable.Range(0,4).Any(i=>canceledRules.Position(0,i)!=-1))
            throw new Exception("Vorgemerkte Deaktivierung wird beim Spielerwechsel nicht übernommen.");
        var inactiveState=new Game();inactiveState.Roll(6);inactiveState.Kinds[0]=PlayerKind.Inactive;
        window.LoadState(OriginalSave.Read(OriginalSave.Write(inactiveState)));
        if(window.TestState.Player!=1||window.TestState.AwaitingMove)
            throw new Exception("Geladener Spielstand bleibt beim inaktiven Spieler hängen.");
        // seg08:04c8..051d: ohne aktiven Spieler wird Spieler 1 Mensch.
        if(!Dialog(204,dialog=>
        {
            for(int current=0;current<6;current++)
            {dialog.Check(102+current*4,false);dialog.Check(103+current*4,false);dialog.Check(104+current*4,true);}
        })||window.TestState.PendingKinds is not {} noneActive||noneActive[0]!=PlayerKind.Human||noneActive.Skip(1).Any(k=>k!=PlayerKind.Inactive))
            throw new Exception("Alle Spieler deaktivieren macht Spieler 1 nicht zum Menschen.");
        window.TestState.SetPosition(1,0,10);
        window.TestNextDie=2;Key(Keys.Space);Key(Keys.Tab);Key(Keys.Enter);
        if(window.TestState.Finished||window.TestState.Player!=0||window.TestState.AwaitingMove||window.TestState.Kinds[1]!=PlayerKind.Inactive)
            throw new Exception("Übernommene Spielerliste gibt den Zug nicht an Spieler 1.");
        if(!Dialog(204,dialog=>{dialog.Check(104+2*4,false);dialog.Check(102+2*4,true);}))
            throw new Exception("Reaktivierung meldet die verzögerte Übernahme nicht.");
        window.TestNextDie=6;Key(Keys.Space);Key(Keys.Tab);Key(Keys.Enter);
        window.TestNextDie=2;Key(Keys.Space);Key(Keys.Tab);Key(Keys.Enter);
        if(window.TestState.Kinds[2]!=PlayerKind.Human||window.TestState.Player!=2||window.TestState.AwaitingMove||
           Enumerable.Range(0,4).Any(i=>window.TestState.Position(2,i)!=-1))
            throw new Exception("Reaktivierung eines Spielers stellt die Zugsteuerung nicht wieder her.");
        window.TestNextDie=6;Key(Keys.Space);Key(Keys.Tab);Key(Keys.Enter);
        if(window.TestState.Position(2,0)!=0||window.TestState.AwaitingMove)
            throw new Exception("Tastaturbedienung nach Spielerreaktivierung funktioniert nicht.");
        foreach(int players in new[]{4,6})
        {
            // Only players 1 and 2 are active, so player 1 moves again after each transition.
            var lifecycle=new Game(players);
            for(int p=2;p<6;p++)lifecycle.Kinds[p]=PlayerKind.Inactive;
            int target=lifecycle.TrackLength/players+5;
            lifecycle.SetPosition(0,0,20);
            lifecycle.SetPosition(0,1,target-2);
            lifecycle.SetPosition(1,0,5);
            lifecycle.SetPosition(1,1,lifecycle.TrackLength+1);
            lifecycle.SetPosition(1,2,lifecycle.TrackLength+3);
            lifecycle.Roll(2);window.LoadState(lifecycle);
            byte[] beforeDisable=OriginalSave.Write(lifecycle);
            if(Dialog(204,dialog=>dialog.Check(108,true),DialogResult.Cancel)||lifecycle.HasPendingChanges||
               !OriginalSave.Write(lifecycle).SequenceEqual(beforeDisable))
                throw new Exception("Abgebrochene Spielerdeaktivierung entfernt Figuren oder merkt sie vor.");
            if(!Dialog(204,dialog=>dialog.Check(107,true))||lifecycle.PendingKinds?[1]!=PlayerKind.Computer)
                throw new Exception("Mensch/Computer-Wechsel wird nicht vorgemerkt.");
            if(!Dialog(204,dialog=>
            {
                if(!dialog.Checked(107))throw new Exception("Spielerdialog zeigt nicht die vorgemerkten Spielertypen.");
                dialog.Check(108,true);
            })||!OriginalSave.Write(lifecycle).SequenceEqual(beforeDisable))
                throw new Exception("Vorgemerkte Deaktivierung verändert Figuren oder Spielstand vor dem Spielerwechsel.");
            Key(Keys.Tab);Key(Keys.Enter);
            byte[] disabled=OriginalSave.Write(lifecycle);
            if(lifecycle.Kinds[1]!=PlayerKind.Inactive||lifecycle.AwaitingMove||lifecycle.Player!=0||lifecycle.Position(0,0)!=22||
               Enumerable.Range(0,4).Any(i=>lifecycle.Position(1,i)!=-1)||
               !disabled.AsSpan(151,4).SequenceEqual(new byte[4])||
               !disabled.AsSpan(271,4).SequenceEqual(new byte[4])||
               !disabled.AsSpan(295,4).SequenceEqual(new byte[4]))
                throw new Exception("Deaktivierter Spieler hinterlässt alte Figuren oder falsche ERG-Felder.");
            string disabledPath=TestOutput.PathFor("deactivated-player-test.erg");
            File.WriteAllBytes(disabledPath,disabled);
            lifecycle=OriginalSave.Read(File.ReadAllBytes(disabledPath));window.LoadState(lifecycle);
            window.TestNextDie=2;Key(Keys.Space);Select(1);Key(Keys.Enter);
            if(lifecycle.Position(0,1)!=target||lifecycle.Player!=0||lifecycle.AwaitingMove)
                throw new Exception("Ehemaliges Feld des deaktivierten Spielers ist nicht normal bespielbar.");
            if(!Dialog(204,dialog=>dialog.Check(106,true)))throw new Exception("Reaktivierung wird nicht vorgemerkt.");
            window.TestNextDie=2;Key(Keys.Space);Select(0);Key(Keys.Enter);
            if(lifecycle.Kinds[1]!=PlayerKind.Human||Enumerable.Range(0,4).Any(i=>lifecycle.Position(1,i)!=-1)||
               lifecycle.Position(0,1)!=target||lifecycle.Player!=1||lifecycle.AwaitingMove)
                throw new Exception("Reaktivierter Spieler erscheint auf alten Bahn-/Zielfeldern oder übernimmt den Wurf.");
            byte[] reactivated=OriginalSave.Write(lifecycle);
            if(!OriginalSave.Write(OriginalSave.Read(reactivated)).SequenceEqual(reactivated))
                throw new Exception("Spielstand nach Reaktivierung enthält kollidierende Felder.");
        }
        // seg08:066e..06d0 / seg04:02f3..0310: Brettwechsel während der Partie.
        var boardChange=new Game();boardChange.SetPosition(0,0,10);boardChange.Roll(2);window.LoadState(boardChange);
        if(!Dialog(205,dialog=>dialog.Check(102,true))||boardChange.PendingBoard!=6||boardChange.PlayerCount!=4)
            throw new Exception("Brettwechsel während der Partie wird nicht vorgemerkt.");
        if(!Dialog(205,_=>{},DialogResult.Cancel))
            throw new Exception("Vorgemerkter Brettwechsel wird nach Abbrechen nicht erneut gemeldet (seg08:06c2).");
        Key(Keys.Tab);Key(Keys.Enter);
        if(boardChange.PlayerCount!=6||boardChange.PendingBoard is not null||boardChange.Player!=1)
            throw new Exception("Vorgemerkter Brettwechsel wird beim Spielerwechsel nicht übernommen.");
        var blockedBoard=new Game(6);blockedBoard.SetPosition(0,0,41);blockedBoard.Roll(2);window.LoadState(blockedBoard);
        if(!Dialog(205,dialog=>dialog.Check(101,true)))throw new Exception("Wechsel auf vier Spieler wird nicht vorgemerkt.");
        bool rejectionShown=false;
        using(var rejectionTimer=new System.Windows.Forms.Timer {Interval=10})
        {
            rejectionTimer.Tick+=(_,_)=>
            {
                var dialog=window.OwnedForms.OfType<OriginalDialog>().LastOrDefault(form=>form.Visible);
                if(dialog is null)return;
                rejectionShown|=dialog.Resource=="DLG_KEINUMSCHALTEN";dialog.DialogResult=DialogResult.OK;
            };
            rejectionTimer.Start();
            Key(Keys.Tab);Key(Keys.Enter);
            var rejectionDeadline=Environment.TickCount64+5000;
            while((!rejectionShown||window.TestModal)&&Environment.TickCount64<rejectionDeadline){Application.DoEvents();Thread.Sleep(5);}
        }
        if(!rejectionShown||blockedBoard.PlayerCount!=6||blockedBoard.PendingBoard is not null||blockedBoard.Player!=1||window.TestModal)
            throw new Exception($"Blockierter vorgemerkter Brettwechsel wird nicht mit DLG_KEINUMSCHALTEN verworfen: shown={rejectionShown}, count={blockedBoard.PlayerCount}, pending={blockedBoard.PendingBoard}, player={blockedBoard.Player}, pos={blockedBoard.Position(0,0)}, awaiting={blockedBoard.AwaitingMove}, {window.TestInputState}, open={string.Join(",",window.OwnedForms.OfType<OriginalDialog>().Select(f=>f.Resource+":"+f.Visible+":"+f.DialogResult))}");
        // Six to four leaving only finished/inactive players 1..4: the original loops forever in
        // seg04:0332 (emulated); the native game ends with the ranking dialog.
        var noTurnLeft=new Game(6,4);
        noTurnLeft.Kinds[0]=PlayerKind.Computer;for(int p=1;p<4;p++)noTurnLeft.Kinds[p]=PlayerKind.Inactive;
        for(int i=0;i<4;i++)noTurnLeft.SetPosition(0,i,48+i);
        noTurnLeft.Winners.Add(0);noTurnLeft.SetPosition(4,0,20);noTurnLeft.Roll(2);window.LoadState(noTurnLeft);
        if(!Dialog(205,dialog=>dialog.Check(101,true))||noTurnLeft.PendingBoard!=4)throw new Exception("Wechsel auf vier Spieler wird nicht vorgemerkt.");
        bool rankingShown=false;
        using(var rankingTimer=new System.Windows.Forms.Timer {Interval=10})
        {
            rankingTimer.Tick+=(_,_)=>
            {
                var dialog=window.OwnedForms.OfType<OriginalDialog>().LastOrDefault(form=>form.Visible);
                if(dialog is null)return;
                rankingShown|=dialog.Resource=="DLG_GEWINNER";dialog.DialogResult=DialogResult.OK;
            };
            rankingTimer.Start();
            Key(Keys.Tab);Key(Keys.Enter);
            var rankingDeadline=Environment.TickCount64+5000;
            while((!rankingShown||window.TestModal||window.TestPlaying)&&Environment.TickCount64<rankingDeadline){Application.DoEvents();Thread.Sleep(5);}
        }
        if(!rankingShown||window.TestPlaying||noTurnLeft.PlayerCount!=4||window.TestModal)
            throw new Exception($"Ohne Spieler für den nächsten Zug endet die Partie nicht: shown={rankingShown}, playing={window.TestPlaying}, count={noTurnLeft.PlayerCount}, finished={noTurnLeft.Finished}, {window.TestInputState}");
        using(var dialog=new OriginalDialog("DLG_REGELN"))
        {
            dialog.Check(101,true);
            if(!dialog.Checked(101))throw new Exception("Original-Regeldialog funktioniert nicht.");
        }
        using(var speed=new OriginalDialog("DLG_SPEED",true))
        {
            speed.Check(102,true);speed.Check(106,true);
            speed.Show();Application.DoEvents();
            using var picture=new Bitmap(speed.Width,speed.Height);
            speed.DrawToBitmap(picture,new Rectangle(Point.Empty,speed.Size));
            picture.Save(TestOutput.PathFor("speed-test.png"));
        }
        var dataDirectory=Path.Combine(AppContext.BaseDirectory,"Data");
        var figures=new Archive(dataDirectory,"GRAPH03");
        var ornaments=new Archive(dataDirectory,"GRAPH04");
        void CheckSprite(Bitmap picture,Bitmap sprite,Point origin)
        {
            for(int row=0;row<sprite.Height;row++)
                for(int column=0;column<sprite.Width;column++)
                    if(picture.GetPixel(origin.X+column,origin.Y+row)!=sprite.GetPixel(column,row))
                        throw new Exception("Spielerfigur oder Würfel entsprechen nicht der Originalgrafik/-position.");
        }
        foreach(int boardSize in new[]{200,400,700})
        {
            int sizeIndex=boardSize==200?0:boardSize==400?1:2;
            Dialog(303,dialog=>{for(int option=0;option<3;option++)dialog.Check(101+option,option==sizeIndex);});
            // seg17:00c7: title at ((panel right + 2 - board size) / 2, 2).
            window.TestStopGame();
            using(var titlePicture=window.TestBoardImage())
            using(var title=new Archive(dataDirectory,"GRAPH01").Bitmap(sizeIndex))
            {
                var titleAt=GameWindow.TitleOrigin(boardSize);
                if(titleAt!=new Point(new[]{26,49,79}[sizeIndex],2))throw new Exception("Titelposition weicht von DS:164c/16cc ab.");
                CheckSprite(titlePicture,title,titleAt);
                if(titlePicture.GetPixel(titleAt.X-1,titleAt.Y).ToArgb()!=Color.White.ToArgb()||titlePicture.GetPixel(titleAt.X,1).ToArgb()!=Color.White.ToArgb())
                    throw new Exception($"Titelbild nicht an der Originalposition ({boardSize}).");
                if(boardSize==700)titlePicture.Save(TestOutput.PathFor("title-test.png"));
            }
            foreach(int players in new[]{4,6})
            {
                int current=players==4?0:5;
                var state=new Game(players,current);state.SetPosition(current,0,5);state.Roll(2);
                state.Names[current]="Spieler & langer Name";
                window.LoadState(state);
                using var picture=window.TestBoardImage();
                var panel=PlayerPanelLayout.ForBoard(boardSize);
                if(picture.GetPixel(panel.Bounds.X,panel.Bounds.Y).ToArgb()!=Color.Red.ToArgb()||
                    picture.GetPixel(panel.Bounds.Right-1,panel.Bounds.Bottom-1).ToArgb()!=Color.Red.ToArgb()||
                    picture.GetPixel(panel.Bounds.X+1,panel.Bounds.Y+1).ToArgb()!=Color.FromArgb(255,255,75).ToArgb()||
                    picture.GetPixel(panel.Bounds.X,panel.Bounds.Y-1).ToArgb()!=Color.White.ToArgb()||
                    picture.GetPixel(panel.Bounds.Right,panel.Name.Y+2).ToArgb()!=Color.White.ToArgb())
                    throw new Exception("Spielerfeld hat falsche Rahmen-/Füllfarbe oder Text läuft über den Rand.");
                using var figure=figures.Bitmap(sizeIndex*20+(players==6?8:0)+current*2);
                using var sadFigure=figures.Bitmap(sizeIndex*20+(players==6?8:0)+current*2+1);
                using var die=ornaments.Bitmap(sizeIndex*32+state.Die-1);
                // seg17:0291/04c1: board at (2,2), pieces at original field origin + 2 for every size;
                // the track piece uses the first figure, yard pieces the second (sad) one.
                using(var boardImage=new Archive(dataDirectory,"GRAPH02").Bitmap(sizeIndex*2+(players==6?1:0)))
                    if(picture.GetPixel(2,2)!=boardImage.GetPixel(0,0)||picture.GetPixel(boardSize+1,boardSize+1)!=boardImage.GetPixel(boardSize-1,boardSize-1))
                        throw new Exception($"Brettbild nicht bei (2,2) ({players}er-Brett, {boardSize}).");
                CheckSprite(picture,figure,BoardLayout.PieceAt(state,sizeIndex,current,0));
                for(int yardPiece=1;yardPiece<4;yardPiece++)CheckSprite(picture,sadFigure,BoardLayout.PieceAt(state,sizeIndex,current,yardPiece));
                picture.Save(TestOutput.PathFor($"player-panel-{boardSize}-{players}.png"));
                CheckSprite(picture,figure,players==4?panel.FourPlayerFigure:panel.SixPlayerFigure);
                CheckSprite(picture,die,panel.Die);
                // seg17:0338 draws the rest marker on every field of DS:1790/17b0:
                // four fields after each start on the four board, three on the six board.
                state.Rules=new Rules(Rest:true);
                window.LoadState(state);
                using var restPicture=window.TestBoardImage();
                using var restMarker=ornaments.Bitmap(sizeIndex*32+(players==6?30:15));
                var track=BoardLayout.Track(players==6,sizeIndex);
                Point RestOrigin(int index)=>new(track[index].X+2,track[index].Y+2);
                bool Marked(int index)
                {
                    var origin=RestOrigin(index);
                    for(int row=0;row<restMarker.Height;row++)
                        for(int column=0;column<restMarker.Width;column++)
                            if(restPicture.GetPixel(origin.X+column,origin.Y+row)!=restMarker.GetPixel(column,row))return false;
                    return true;
                }
                int stride=track.Length/players,offset=players==6?3:4;
                for(int p=0;p<players;p++)
                    if(!Marked(p*stride+offset)||Marked(p*stride+(players==6?4:3)))
                        throw new Exception($"Ruhefeld-Markierung auf dem {players}er-Brett ({boardSize}) nicht auf dem Originalfeld {p*stride+offset+1}.");
                if(boardSize==700)restPicture.Save(TestOutput.PathFor($"rest-fields-{players}.png"));
                // seg17:06b1..0840: a piece on a rest field gets the four corner marks drawn over it.
                var onRest=new Game(players,current){Rules=new Rules(Rest:true)};
                int restField=current*stride+offset;
                onRest.SetPosition(current,0,(restField-current*stride+track.Length)%track.Length);onRest.Roll(2);
                window.LoadState(onRest);
                using var cornerPicture=window.TestBoardImage();
                using var corner=ornaments.Bitmap(sizeIndex*32+(players==6?31:16));
                foreach(var mark in BoardLayout.RestCorners(players==6,sizeIndex))
                    CheckSprite(cornerPicture,corner,new(track[restField].X+mark.X+2,track[restField].Y+mark.Y+2));
                // seg17:1558: the keyboard selection is a black 1-pixel square of the field size
                // whose top-left corner is the piece's drawing position (field origin + 2).
                var choice=new Game(players,current);
                for(int piece=0;piece<4;piece++)choice.SetPosition(current,piece,2+piece*4);
                choice.Roll(1);
                window.LoadState(choice);
                int fieldSize=BoardLayout.FieldSize(players==6,sizeIndex);
                bool Framed(Bitmap picture,int piece)
                {
                    var at=BoardLayout.PieceAt(choice,sizeIndex,current,piece);
                    for(int d=0;d<fieldSize;d++)
                        foreach(var point in new Point[]{new(at.X+d,at.Y),new(at.X+d,at.Y+fieldSize-1),new(at.X,at.Y+d),new(at.X+fieldSize-1,at.Y+d)})
                            if(picture.GetPixel(point.X,point.Y).ToArgb()!=Color.Black.ToArgb())return false;
                    return true;
                }
                using(var unselected=window.TestBoardImage())
                    if(Enumerable.Range(0,4).Any(piece=>Framed(unselected,piece)))
                        throw new Exception($"Figur ohne Auswahl bereits umrahmt ({players}er-Brett, {boardSize}).");
                int first=window.TestSelectionIndex;
                Key(Keys.Tab);
                using(var selection=window.TestBoardImage())
                {
                    if(boardSize==700&&players==4)selection.Save(TestOutput.PathFor("selection-test.png"));
                    if(!Framed(selection,first)||Enumerable.Range(0,4).Any(piece=>piece!=first&&Framed(selection,piece)))
                        throw new Exception($"Auswahlrahmen nicht wie seg17:1558 um Figur {first+1} ({players}er-Brett, {boardSize}).");
                }
                Key(Keys.Tab);
                using(var next=window.TestBoardImage())
                    if(!Framed(next,(first+1)%4)||Framed(next,first))
                        throw new Exception($"Tab verschiebt den Auswahlrahmen nicht ({players}er-Brett, {boardSize}).");
            }
        }
        // seg09:0b65/seg04:0412: DS:1d88 remembers the last selected piece across moves and loading;
        // the first Tab shows it again or the next movable piece after it. Only a new game resets it.
        var remember=new Game();
        for(int piece=0;piece<4;piece++)remember.SetPosition(0,piece,2+piece*4);
        remember.Roll(1);window.LoadState(remember);
        int remembered=(window.TestSelectionIndex+1)%4;
        Key(Keys.Tab);Key(Keys.Tab);
        if(window.TestSelectionIndex!=remembered)throw new Exception("Tab wählt nicht die nächste Figur.");
        Key(Keys.Enter);
        if(remember.Position(0,remembered)!=3+remembered*4)throw new Exception("Eingabe zieht nicht die gewählte Figur.");
        var again=new Game();
        for(int piece=0;piece<4;piece++)again.SetPosition(0,piece,2+piece*4);
        again.Roll(1);window.LoadState(again);Key(Keys.Tab);Key(Keys.Enter);
        if(again.Position(0,remembered)!=3+remembered*4)
            throw new Exception("Erste Auswahl beginnt nicht bei der zuletzt gewählten Figur (DS:1d88).");
        var blocked=new Game();
        for(int piece=0;piece<4;piece++)blocked.SetPosition(0,piece,piece==remembered?-1:2+piece*4);
        blocked.Roll(1);window.LoadState(blocked);Key(Keys.Tab);
        if(window.TestSelectionIndex!=(remembered+1)%4)
            throw new Exception("Erste Auswahl überspringt eine unbewegliche gemerkte Figur nicht zyklisch.");
        window.TestStopGame();window.PrepareInteractionTest();
        if(window.TestSelectionIndex!=0)throw new Exception("Neues Spiel setzt die gemerkte Figur nicht auf Figur 1 zurück.");
        // seg09:086e/0915/0ca1: board clicks act on button down (left or right), a roll needs
        // y>8, and the original cursors follow the state (die, figure, mouse).
        Dialog(303,dialog=>{for(int option=0;option<3;option++)dialog.Check(101+option,option==2);});
        // Checks the state between button down and up: the original acts on the press.
        void Mouse(uint down,uint up,nuint keys,Point at,Func<bool> afterDown,string failure)
        {
            nint position=(at.Y<<16)|(at.X&0xffff);
            var surface=window.TestSurface;
            void Settle()
            {
                var deadline=Environment.TickCount64+10000;
                while(window.TestBusy&&Environment.TickCount64<deadline){Application.DoEvents();Thread.Sleep(5);}
                if(window.TestBusy)throw new Exception("Animation nach Mausklick wird nicht beendet.");
            }
            SendMessageW(surface.Handle,0x200,0,position);
            SendMessageW(surface.Handle,down,keys,position);Settle();
            if(!afterDown())throw new Exception($"{failure} (Zeiger {window.TestCursorName}).");
            SendMessageW(surface.Handle,up,0,position);Settle();
        }
        var clicks=new Game();clicks.SetPosition(0,0,10);window.LoadState(clicks);
        if(window.TestCursorName!="MAUSWUERFEL")throw new Exception($"Vor dem Würfeln fehlt der Original-Würfelzeiger ({window.TestCursorName}).");
        window.TestNextDie=3;
        Mouse(0x201,0x202,1,new Point(300,8),()=>!clicks.AwaitingMove,"Klick bei y<=8 würfelt");
        Mouse(0x207,0x208,0x10,new Point(300,300),()=>!clicks.AwaitingMove,"Mittlere Maustaste würfelt");
        if(clicks.AwaitingMove||window.TestNextDie!=3)throw new Exception("Klick bei y<=8 oder mittlere Maustaste würfelt beim Loslassen.");
        Mouse(0x204,0x205,2,new Point(300,9),()=>clicks.AwaitingMove&&clicks.Die==3&&window.TestCursorName=="MAUSFIG",
            "Rechte Maustaste würfelt nicht beim Drücken oder der Figurenzeiger fehlt");
        var clickedPiece=BoardLayout.Center(clicks,2,0,0);
        Mouse(0x201,0x202,1,new Point((int)clickedPiece.X,(int)clickedPiece.Y),
            ()=>clicks.Position(0,0)==13&&clicks.Player==1&&window.TestCursorName=="MAUSWUERFEL","Linksklick auf die Figur zieht nicht beim Drücken");
        clicks.Kinds[1]=PlayerKind.Computer;window.LoadState(clicks);
        if(window.TestCursorName!="MAUSMAUS")throw new Exception("Während der Computer zieht, fehlt der Original-Mauszeiger.");
        window.TestStopGame();window.LoadState(new Game());window.TestStopGame();
        SendMessageW(window.TestSurface.Handle,0x200,0,(100<<16)|100);
        if(window.TestCursorName!="MAUSMAUS")throw new Exception("Auf dem Titelbild fehlt der Original-Mauszeiger.");
        window.TestLoadExample();
        WindowChrome.GetClientRect(window.Handle,out var client);
        File.WriteAllText(TestOutput.PathFor("window-metrics.txt"),$"Window={window.Size}; Client={window.ClientSize}; Win32Client={client.Right},{client.Bottom}; DPI={WindowChrome.GetDpiForWindow(window.Handle)}; Style={WindowChrome.GetWindowLongW(window.Handle,-16):X}; ExStyle={WindowChrome.GetWindowLongW(window.Handle,-20):X}");
        if(window.TestState.Position(0,3)!=41 || window.TestState.Player!=1)throw new Exception("Original-Beispiel wurde nicht geladen.");
        File.WriteAllText(TestOutput.PathFor("ui-test.txt"),
            "PASS: native menu, no Topmost, keyboard roll, Tab selection, Enter move, rule control.\nPASS: player 1 starts, first-active fallback, no extra sound on ordinary moves/yard exit, capture sound gates at all speeds, muted effects.\nPASS: title screen at the original position (seg17:00c7) in all three sizes.\nPASS: player panel, current figure and separate dice at original coordinates in all three sizes on both boards; rest markers on original fields (DS:1790/17b0).\nPASS: rule/player/board changes during a game are deferred with DLG_SPIELERAENDERUNG and applied at the next player transition (seg04:0022); dialogs show pending values; cancel; player 1 forced human; blocked board change reports DLG_KEINUMSCHALTEN; a change leaving no player for the turn ends the game; inactive saved turn.\nNot a certificate of full original parity.\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: help menu and F1, topic extraction, context targets, inline hotspots (colour, mouse, Tab/Enter), umlauts, full-text search, empty results, back history, unchanged game state and modal restoration.\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: keyboard selection as black field-size square at the piece origin in all sizes on both boards (seg17:1558); remembered piece survives moves and loading, skips unmovable pieces, reset by a new game (DS:1d88, seg04:0412).\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: original cursors MAUSWUERFEL/MAUSFIG/MAUSMAUS by state; board clicks on left/right button down, middle ignored, no roll at y<=8 (seg09:086e/0915/0ca1).\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: exact client sizes through size menu, native resize loop, maximize to fitting board, minimize/restore, unchanged game state.\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: capture victim turns sad on its field until the attacker arrives (seg04:13da/0e3e); capture waits for the current sample even when sample 3 is speed-gated; moving piece drawn over passed pieces (seg05:020c); die erased after moves and next-player transitions, kept for failed entry attempts (seg04:0b3e/0022, seg09:0202).\n"+
            "PASS: ordered track/yard capture sounds and goal steps at all speeds, computer capture speed, capture sound wait, victim hidden until return sound completes, returned sprite, six-player capture/goal, four wrap-step sprites.\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: capture completion waits for last sample at speeds 0/1/2; dice sound ends before speech; fast speech modes wait for current sample without starting a spoken number.\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: keyboard entry attempts with packed/unpacked goal on both boards, third failed attempt passes, next player can roll and move.\n");
        File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: player dialog cancel and deferred type changes; disable at the next transition clears track/goal pieces and ERG fields; zero-field save loads; reactivation starts in yard without field collisions on both boards.\n");
    }
}
