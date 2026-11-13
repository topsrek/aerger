using System.Drawing.Drawing2D;

namespace Aerger;

public sealed class GameWindow : Form
{
    private readonly BoardPanel surface = new() { Dock = DockStyle.Fill };
    private readonly Archive boards;
    private readonly Archive figures;
    private readonly Archive ornaments;
    private readonly Archive titles;
    private readonly Dictionary<(string,int), Bitmap> images = [];
    private readonly System.Windows.Forms.Timer timer = new();
    private nint nativeMenu;
    private readonly Dictionary<int, Action> commands = [];
    private readonly string directory = Path.Combine(AppContext.BaseDirectory, "Data");
    private Game game = new();
    private bool playing;
    private bool modal;
    private int boardSize = 700;
    private bool layoutReady, adjustingWindow, sizingWindow, minimizedWindow;
    private int boardPlayers = 4;
    private int computerSpeed = 1, humanSpeed = 1;
    private bool speech = true, effects = true;
    // seg04:0412: DS:1d89 (selection shown) as selected>=0, DS:1d88 (last selected piece)
    // as selectionIndex. Only a new game resets the index (seg02:0d2d); it survives turns,
    // players and loading.
    private int selected = -1, selectionIndex;
    private int announcedWinners;
    private readonly bool automated;
    private readonly GameSound sound;
    private readonly CancellationTokenSource lifetime = new();
    private bool busy;
    private bool closingApproved;
    private bool repeatRollDelay;
    private int shownDie;
    // seg02:0ac5 Randomize at startup; dice and the new-game draw use the original generator.
    private readonly OriginalRandom random = new(OriginalRandom.SeedFromClock(DateTime.Now));
    internal uint TestRandomSeed { get => random.Seed; set => random.Seed = value; }
    // Event log in the format of research/emulate_timeline.py (automation only).
    internal List<string>? TestTimeline;
    private readonly System.Diagnostics.Stopwatch timelineClock = new();
    // The game stops itself after TestRollLimit rolls: at speed 3 it can run on inside one DoEvents.
    internal int TestRollLimit;
    private void Timeline(string text)
    {
        if(TestTimeline is null)return;
        TestTimeline.Add($"{timelineClock.ElapsedMilliseconds} {text}");
        if(text.StartsWith("rolled ")&&TestTimeline.Count(line=>line.Contains(" rolled "))>=TestRollLimit)playing=false;
    }
    private int FieldCode(int player,int position)=>position<0?101:position>=game.TrackLength?201+position-game.TrackLength:game.Square(player,position)+1;
    internal void TestStartComputerGame(uint seed,int speed,int players,Rules rules,bool sounds)
    {
        playing=false;boardPlayers=players;computerSpeed=speed;speech=sounds;effects=sounds;game.Rules=rules;
        for(int p=0;p<6;p++)game.Kinds[p]=p<players?PlayerKind.Computer:PlayerKind.Inactive;
        random.Seed=seed;TestTimeline=[];timelineClock.Restart();
        NewGame();
    }
    private int movingPlayer = -1, movingPiece = -1, movingPosition, movingTarget;
    private readonly HashSet<(int Player,int Piece)> hiddenCapturedPieces = [];
    internal int? TestNextDie { get; set; }
    internal int TestKeyCount { get; private set; }
    internal Keys TestLastKey { get; private set; }
    internal string TestInputState => $"modal={modal}, sizing={sizingWindow}, busy={busy}, selected={selected}, closing={closingApproved}, lastKey={TestLastKey}, keys={TestKeyCount}";
    private int Speed => game.Kinds[game.Player] == PlayerKind.Computer ? computerSpeed : humanSpeed;
    protected override bool ShowWithoutActivation => automated;

    public GameWindow(bool automated = false)
    {
        this.automated=automated;
        Text = "Mensch ärgere Dich nicht! V1.0";
        using var iconStream = typeof(GameWindow).Assembly.GetManifestResourceStream("Aerger.Reference.original.ico")!;
        Icon = new Icon(iconStream);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        TopMost = false;
        StartPosition = FormStartPosition.CenterScreen;
        boards = new(directory, "GRAPH02"); figures = new(directory, "GRAPH03");
        ornaments = new(directory, "GRAPH04"); titles = new(directory, "GRAPH01");
        sound = new(directory);
        sound.SamplePlayed += sample => Timeline($"sound {sample}");
        for (int p = 1; p < 6; p++) game.Kinds[p] = PlayerKind.Computer;
        LoadSettings();
        Controls.Add(surface);
        BuildMenu();
        WindowChrome.OriginalFrame(Handle);
        boardSize=Math.Min(boardSize,WindowSizing.LargestFittingBoard(AvailableWindowClient()));
        ResizeBoard();
        layoutReady=true;
        surface.Paint += PaintBoard;
        // seg09:086e handles WM_LBUTTONDOWN; seg09:0915 forwards WM_RBUTTONDOWN to it.
        surface.MouseDown += async (_, e) => { if (e.Button is MouseButtons.Left or MouseButtons.Right) await MouseAction(e.Location); };
        // seg09:0ca1 (WM_MOUSEMOVE) selects the original cursor for the current state.
        surface.MouseMove += (_, _) => UpdateCursor();
        timer.Tick += async (_, _) => await ComputerStep();
        timer.Start();
        Shown += (_, _) => { if (speech) sound.Play(1, !automated); };
        FormClosing += async (_, e) =>
        {
            if(closingApproved)return;
            e.Cancel=true;
            if(!ConfirmEnd())return;
            closingApproved=true;lifetime.Cancel();timer.Stop();computerWake?.Cancel();
            if(effects)await Task.Delay(sound.Play(4,!automated));
            if(!IsDisposed)Close();
        };
        RefreshGame();
    }
    private Bitmap Image(string name, int index)
    {
        if (!images.TryGetValue((name,index), out var bitmap))
        { bitmap = (name switch { "board"=>boards,"figure"=>figures,"title"=>titles,_=>ornaments }).Bitmap(index); images.Add((name,index),bitmap); }
        return bitmap;
    }
    private void BuildMenu()
    {
        nativeMenu=WindowChrome.CreateMenu();
        nint Group(string name)
        {var group=WindowChrome.CreateMenu();WindowChrome.AppendMenuW(nativeMenu,0x10,(nuint)group,name);return group;}
        void Item(nint parent,int id,string text,Action action)
        {WindowChrome.AppendMenuW(parent,0,(nuint)id,text);commands.Add(id,action);}
        void Separator(nint parent)=>WindowChrome.AppendMenuW(parent,0x800,0,null);
        var file=Group("&Datei");
        Item(file,101,"Spielstand &speichern",SaveGame);
        Item(file,102,"Spielstand &laden",LoadGame);
        Item(file,103,"&Einstellungen speichern",SaveSettings);
        Separator(file);Item(file,24340,"&Beenden",Close);Separator(file);
        Item(file,105,"&Info",()=>{if(effects)sound.Play(8,!automated);ShowOriginal("DLG_INFO");});
        var play=Group("&Spiel");
        Item(play,201,"&Neues Spiel",NewGame);
        Item(play,202,"Spiel be&enden",()=>{if(playing&&ConfirmEnd())EndGame();});
        Separator(play);Item(play,203,"&Gewinner...",ShowWinners);Item(play,204,"&Spieler...",PlayersDialog);
        Item(play,205,"Spiel&brett",BoardDialog);Item(play,206,"Spiel&regeln",RulesDialog);
        var options=Group("&Optionen");
        Item(options,301,"&Sound",SoundDialog);Item(options,302,"&Geschwindigkeit",SpeedDialog);Item(options,303,"&Fenstergröße",SizeDialog);
        Item(Group("&Hilfe"),901,"&Inhalt",HelpDialog);
        WindowChrome.SetMenu(Handle,nativeMenu);
        WindowChrome.DrawMenuBar(Handle);
    }
    private void SaveGame()
    {
        if (!playing) return;
        modal = true;
        try
        {
            using var dialog = new SaveFileDialog { Filter = "Spielstand (*.erg)|*.erg|Alle Dateien (*.*)|*.*", DefaultExt = "erg", AddExtension = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllBytes(dialog.FileName, OriginalSave.Write(game));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, Text); }
        finally { modal = false; }
    }
    private void LoadGame()
    {
        modal = true;
        try
        {
            using var dialog = new OpenFileDialog { Filter = "Spielstand (*.erg)|*.erg|Alle Dateien (*.*)|*.*", DefaultExt = "erg", InitialDirectory = directory };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var loaded = OriginalSave.Read(File.ReadAllBytes(dialog.FileName));
            // seg12:05b5 verwirft beim Laden nur vorgemerkte Spieleränderungen;
            // vorgemerkte Regel- und Brettwechsel gelten beim nächsten Spielerwechsel.
            if(playing){loaded.PendingRules=game.PendingRules;loaded.PendingBoard=game.PendingBoard;}
            LoadState(loaded);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, Text); }
        finally { modal = false; RefreshGame(); }
    }
    private DialogResult ShowOriginal(string name, Action<OriginalDialog>? prepare = null, Action<OriginalDialog>? apply = null)
    {
        bool previousModal = modal;
        modal = true;
        try
        {
            using var dialog = new OriginalDialog(name, automated);
            prepare?.Invoke(dialog);
            var result = dialog.ShowDialog(this);
            if (result == DialogResult.OK) apply?.Invoke(dialog);
            return result;
        }
        finally { modal = previousModal; RefreshGame(); }
    }
    private bool ConfirmEnd() => !playing || ShowOriginal("DLG_NEU_FRAGE") == DialogResult.OK;
    // seg04:15ae: Spielende übernimmt vorgemerkte Regel-, Spieler- und Brettänderungen.
    private void EndGame()
    {
        playing=false;
        if(game.ApplyPendingAfterGame() is int board) boardPlayers=board;
        RefreshGame();
    }
    private void ShowChangeNotice() => ShowOriginal("DLG_SPIELERAENDERUNG");
    private void NewGame()
    {
        if (!ConfirmEnd()) return;
        if (playing) EndGame();
        var previous = game;
        var active = Enumerable.Range(0,boardPlayers).Where(p=>previous.Kinds[p] != PlayerKind.Inactive).ToArray();
        if (active.Length == 0) { PlayersDialog(); return; }
        // seg02:0d3d draws Random(player count) for the starting player; the native game
        // starts with the first active player (user decision) but keeps the generator in step.
        random.Next(boardPlayers);
        game = new Game(boardPlayers, active[0]) { Rules = previous.Rules };
        Array.Copy(previous.Kinds,game.Kinds,6); Array.Copy(previous.Names,game.Names,6);
        playing = true; selected = -1; selectionIndex = 0; announcedWinners = 0; shownDie = 0; repeatRollDelay=false;
        RefreshGame();
    }
    // seg08:02db..0307: der Dialog zeigt vorgemerkte Werte, falls vorhanden.
    private void PlayersDialog() => ShowOriginal("DLG_SPIELER", d =>
    {
        var names=game.PendingNames??game.Names;var kinds=game.PendingKinds??game.Kinds;
        for (int p=0;p<6;p++) { d.Value(101+p*4,names[p]); d.Check(102+p*4+(int)kinds[p],true); }
    }, d =>
    {
        var names=new string[6];var kinds=new PlayerKind[6];
        for (int p=0;p<6;p++)
        {
            names[p]=d.Value(101+p*4);
            kinds[p]=d.Checked(102+p*4) ? PlayerKind.Human : d.Checked(103+p*4) ? PlayerKind.Computer : PlayerKind.Inactive;
        }
        // seg08:04c8..051d: ohne aktiven Spieler auf dem aktuellen Brett wird Spieler 1 Mensch.
        if(!Enumerable.Range(0,boardPlayers).Any(p=>kinds[p]!=PlayerKind.Inactive))kinds[0]=PlayerKind.Human;
        if(playing){game.SetPendingPlayers(names,kinds);ShowChangeNotice();return;}
        for (int p=0;p<6;p++){game.Names[p]=names[p];game.SetPlayerKind(p,kinds[p]);}
        game.EnsureActivePlayer();
        selected=-1;
    });
    private void RulesDialog() => ShowOriginal("DLG_REGELN", d=>
    {
        var r=game.PendingRules??game.Rules;
        bool[] values=[r.Rest,r.Capture,r.Hard,r.Absolute,r.NoJump,r.EnterOnOne,r.RepeatOnOne];
        for(int i=0;i<7;i++) d.Check(101+i,values[i]);
    },d=>
    {
        var rules=new Rules(d.Checked(101),d.Checked(102),d.Checked(103),d.Checked(104),d.Checked(105),d.Checked(106),d.Checked(107));
        if(playing){game.PendingRules=rules;ShowChangeNotice();return;}
        game.Rules=rules;selected=-1;
    });
    // seg08:066e..06d0: während einer Partie wird nur der Wechsel vorgemerkt; die
    // Meldung erscheint nach dem Dialog, solange ein Wechsel vorgemerkt ist (auch nach Abbrechen).
    private void BoardDialog()
    {
        ShowOriginal("DLG_SPIELBRETT",d=>d.Check(boardPlayers==4?101:102,true),d=>
        {
            int chosen=d.Checked(101)?4:6;
            if (chosen==boardPlayers) return;
            if (playing) {game.PendingBoard=chosen;return;}
            boardPlayers=chosen;
            selected=-1;
        });
        if(playing&&game.PendingBoard is not null)ShowChangeNotice();
    }
    private void SoundDialog() => ShowOriginal("DLG_SOUND", d=> { d.Check(101,speech);d.Check(102,effects); }, d=> {speech=d.Checked(101);effects=d.Checked(102);});
    private void SpeedDialog() => ShowOriginal("DLG_SPEED",d=> {d.Check(101+computerSpeed,true);d.Check(105+humanSpeed,true);},d=>
    {computerSpeed=Enumerable.Range(0,4).First(i=>d.Checked(101+i));humanSpeed=Enumerable.Range(0,4).First(i=>d.Checked(105+i));});
    private void SizeDialog() => ShowOriginal("DLG_SIZE",d=>d.Check(boardSize==200?101:boardSize==400?102:103,true),d=>
    {boardSize=d.Checked(101)?200:d.Checked(102)?400:700;ResizeBoard();});
    private void ShowWinners()
    {
        var result=ShowOriginal("DLG_GEWINNER",d=>
        {for(int i=0;i<6;i++) d.Value(201+i,i<game.Winners.Count?game.Names[game.Winners[i]]:"");});
        if(playing&&(result==DialogResult.Abort || game.Finished))EndGame();
    }
    private void HelpDialog()
    {
        bool previousModal=modal;
        modal=true;
        try
        {
            using var form=new HelpWindow();
            form.ShowDialog(this);
        }
        finally {modal=previousModal;}
    }
    private void ResizeBoard()
    {
        adjustingWindow=true;
        try
        {
            MinimumSize=Size.Empty;MaximumSize=Size.Empty;
            MinimumSize=WindowChrome.OuterForClient(Handle,WindowSizing.ClientForBoard(200));
            MaximumSize=WindowChrome.OuterForClient(Handle,WindowSizing.ClientForBoard(700));
            var client=WindowSizing.ClientForBoard(boardSize);
            WindowChrome.ResizeClient(Handle,client.Width,client.Height);
            surface.Invalidate();
        }
        finally {adjustingWindow=false;}
    }
    private Size AvailableWindowClient()
    {
        var work=Screen.FromHandle(Handle).WorkingArea;
        var frame=WindowChrome.OuterForClient(Handle,Size.Empty);
        return new(work.Width-frame.Width,work.Height-frame.Height);
    }
    private void FitWindow(bool maximize=false)
    {
        if(!layoutReady||adjustingWindow)return;
        Size available=ClientSize;
        if(maximize)
        {
            available=AvailableWindowClient();
            adjustingWindow=true;
            try {WindowState=FormWindowState.Normal;}
            finally {adjustingWindow=false;}
        }
        boardSize=WindowSizing.LargestFittingBoard(available);
        ResizeBoard();
        if(maximize)
        {
            var work=Screen.FromHandle(Handle).WorkingArea;
            Location=new(work.Left+(work.Width-Width)/2,work.Top+(work.Height-Height)/2);
        }
    }
    protected override void OnHandleCreated(EventArgs e)
    {base.OnHandleCreated(e);WindowChrome.OriginalFrame(Handle);if(nativeMenu!=0)WindowChrome.SetMenu(Handle,nativeMenu);}
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x81)WindowChrome.SetWindowTheme(m.HWnd,"","");
        if(m.Msg==0x112&&(m.WParam&0xfff0)==0xf030&&layoutReady)
        {FitWindow(true);return;}
        if(m.Msg==0x231)sizingWindow=true;
        if(m.Msg==0x232)
        {sizingWindow=false;FitWindow();}
        if(m.Msg==0x111&&m.LParam==0&&commands.TryGetValue((int)(m.WParam&0xffff),out var command))
        {if (!busy && !closingApproved) {modal=false;command();}return;}
        if(m.Msg==0x211)modal=true;
        if(m.Msg==0x212)modal=false;
        base.WndProc(ref m);
        if(m.Msg==0x5&&layoutReady&&!adjustingWindow)
        {
            if(m.WParam==1)minimizedWindow=true;
            else if(m.WParam==2||minimizedWindow)
            {
                bool maximize=m.WParam==2;
                minimizedWindow=false;
                // WinForms completes its restore bounds after WM_SIZE returns.
                // Reapply the selected size afterwards, without choosing a smaller
                // board from the temporary client rectangle (which omits the menu).
                BeginInvoke((Action)(()=>
                {
                    if(IsDisposed||closingApproved||sizingWindow||WindowState==FormWindowState.Minimized)return;
                    if(maximize)FitWindow(true);else ResizeBoard();
                }));
            }
        }
    }
    private async Task Pause(int milliseconds)
    { if (milliseconds > 0) await Task.Delay(milliseconds, lifetime.Token); }
    // seg05:0022 schedules each frame/step from the clock reading of the previous one, polled
    // every millisecond. Fixed deadlines from the animation start keep the native frames from
    // accumulating the ~1 ms overshoot of every Task.Delay.
    private readonly System.Diagnostics.Stopwatch animationClock = System.Diagnostics.Stopwatch.StartNew();
    private async Task PauseUntil(long deadline)
    {
        long wait = deadline - animationClock.ElapsedMilliseconds;
        if (wait > 0) await Task.Delay((int)wait, lifetime.Token);
    }
    private async Task ComputerStep()
    {
        if (!playing || modal || busy || sizingWindow || game.Finished || game.Kinds[game.Player] != PlayerKind.Computer) return;
        if (!game.AwaitingMove) await Roll();
        else
        {
            var moves = game.LegalMoves();
            if (moves.Length == 0) game.Pass();
            else await PlayMove(ComputerStrategy.Choose(game));
            RefreshGame();
        }
    }
    // seg05:0232 compares GetTickCount with its deadline on every idle pass. WM_TIMER keeps the
    // 15.6 ms tick, so a precise Task.Delay wakes the computer; the timer (a little later, then
    // periodic) only retries when a dialog, an animation or a window sizing blocked the step.
    private CancellationTokenSource? computerWake;
    private async void WakeComputer(int delay)
    {
        computerWake?.Cancel();
        var wake = computerWake = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try { await Task.Delay(Math.Max(1, delay), wake.Token); }
        catch (OperationCanceledException) { return; }
        if (wake == computerWake) await ComputerStep();
    }
    private async Task Roll()
    {
        if(!playing||busy||closingApproved||game.AwaitingMove||game.Finished) return;
        busy = true;
        try
        {
            int rollingPlayer=game.Player;
            int speed = Speed;
            Timeline("roll");
            if (effects && speed < 2) sound.Play(2, !automated);
            // seg04:051c: 18 dice frames; DS:0124 contains 65/25/10/0 ms.
            long frameDeadline = animationClock.ElapsedMilliseconds;
            for (int frame = 0; frame < 18; frame++)
            {
                // seg05:00b6..0122 changes the face for frames 1..10, then holds it.
                if(frame<10){shownDie=random.Next(6)+1;if(automated&&TestNextDie.HasValue)shownDie=TestNextDie.Value;Timeline($"face {shownDie}");}
                surface.Invalidate(); frameDeadline += new[]{65,25,10,0}[speed]; await PauseUntil(frameDeadline);
            }
            game.Roll(shownDie); TestNextDie=null; selected=-1;
            surface.Invalidate();
            if (speech)
            {
                // seg04:0673 waits before the speed-gated spoken number.
                await Pause(sound.RemainingMilliseconds);
                if (speed < 2) sound.Play(8 + game.Die, !automated);
                await Pause(sound.RemainingMilliseconds);
            }
            // seg04:0022 (next player) erases the die via seg17:0c5c; a retry or a six
            // without a move (0871) leaves it on screen.
            if(game.LegalMoves().Length==0) {int transitions=game.Transitions;game.Pass();if(game.Transitions!=transitions)shownDie=0;}
            repeatRollDelay=!game.AwaitingMove&&game.Player==rollingPlayer;
            Timeline($"rolled {game.Die} "+(game.AwaitingMove?"move":$"next {game.Player+1}"));
        }
        catch (OperationCanceledException) { }
        finally { busy = false; if (!IsDisposed) RefreshGame(); }
    }
    private async Task PlayMove(int piece)
    {
        if (busy) return;
        busy = true;
        try
        {
            int speed = Speed, from = game.Position(game.Player,piece), to = game.Target(piece);
            bool capture = game.IsCapture(piece);
            selected=-1; movingPlayer=game.Player; movingPiece=piece;
            movingPosition=from;movingTarget=to;
            if(capture)
            {
                for(int player=0;player<game.PlayerCount;player++)
                    for(int other=0;other<4;other++)
                    {
                        int position=game.Position(player,other);
                        if(player!=game.Player&&game.Kinds[player]!=PlayerKind.Inactive&&position>=0&&position<game.TrackLength&&
                            game.Square(player,position)==game.Square(game.Player,to))
                            hiddenCapturedPieces.Add((player,other));
                    }
                surface.Invalidate();
                // seg04:12f6/130a allows speed 2 for track captures;
                // seg04:0d5a/0d6e allows only 0/1 when leaving the yard.
                // Both paths then draw the victim's sad figure on its field (13da/0e3e)
                // and, with effects on, wait for the current sample even if sample 3
                // was suppressed by the speed gate (13e6/0e4a).
                if(effects){if(speed<(from<0?2:3))sound.Play(3,!automated);await Pause(sound.RemainingMilliseconds);}
            }
            else if(from<0&&effects&&speed<3)sound.Play(5,!automated);
            if(from<0)
            {
                // Yard exits are placed directly (seg04:0f64), without a step tick.
                movingPosition=to;
                surface.Invalidate();
            }
            else
            {
                Timeline($"move {movingPlayer+1} {FieldCode(movingPlayer,from)}");
                long stepDeadline = animationClock.ElapsedMilliseconds;
                for (int at=from+1;at<=to;at++)
                {
                    movingPosition=at;
                    Timeline($"step {FieldCode(movingPlayer,at)}");
                    if(effects&&speed<3)sound.Play(6,!automated);
                    surface.Invalidate();
                    stepDeadline += new[]{500,200,100,0}[speed]; await PauseUntil(stepDeadline);
                }
            }
            if (capture && effects && speed < 2) await Pause(sound.Play(4, !automated));
            // seg04:08fd waits for the current sample (its DS:173e duration) even when no
            // return sample is started, e.g. for sample 3 of a yard capture at speed 2.
            if (capture && effects) await Pause(sound.RemainingMilliseconds);
            // Finish the turn after the return sound; until then the moving
            // piece stays at its destination and the victim remains hidden.
            // seg04:0b3e erases the die after every move, before the next-player check.
            int mover=movingPlayer;
            game.Move(piece); repeatRollDelay=false; movingPlayer=-1; movingPiece=-1; shownDie=0;
            Timeline(from<0?$"place {mover+1} {FieldCode(mover,to)} {game.Player+1}":$"moved {game.Player+1}");
        }
        catch (OperationCanceledException) { }
        finally { busy=false;movingPlayer=-1;movingPiece=-1;hiddenCapturedPieces.Clear();if(!IsDisposed)RefreshGame(); }
        if(!IsDisposed && !closingApproved && game.Winners.Count>announcedWinners)
        {announcedWinners=game.Winners.Count;if(effects)sound.Play(7,!automated);ShowWinners();}
    }
    private static Cursor LoadCursor(string name)
    {
        using var stream=typeof(GameWindow).Assembly.GetManifestResourceStream($"Aerger.Reference.Cursors.{name}.cur")!;
        return new Cursor(stream);
    }
    private readonly Cursor mouseCursor=LoadCursor("MAUSMAUS"),diceCursor=LoadCursor("MAUSWUERFEL"),figureCursor=LoadCursor("MAUSFIG");
    // seg09:0cad..0cdc and the SetCursor calls in seg04: the mouse on the title screen and
    // while the computer plays, the die before a human roll, the figure while a move is pending.
    private Cursor StateCursor=>!playing||game.Finished||game.Kinds[game.Player]!=PlayerKind.Human?mouseCursor:game.AwaitingMove?figureCursor:diceCursor;
    private void UpdateCursor(){var cursor=StateCursor;if(surface.Cursor!=cursor)surface.Cursor=cursor;}
    internal string TestCursorName=>surface.Cursor==mouseCursor?"MAUSMAUS":surface.Cursor==diceCursor?"MAUSWUERFEL":surface.Cursor==figureCursor?"MAUSFIG":"anderer";
    internal Control TestSurface=>surface;
    private async Task MouseAction(Point at)
    {
        if(!playing||modal||busy||sizingWindow||closingApproved||game.Finished||game.Kinds[game.Player]!=PlayerKind.Human) return;
        // seg09:08a7..08b8: a click rolls only below y=8 of the client area.
        if(!game.AwaitingMove) {if(at.Y>8)await Roll();return;}
        // seg09:08bf..08fa: seg07:003a maps the point to an original field, seg13:1140 accepts
        // only a legal move of the current player's piece on that field.
        int piece=BoardLayout.PieceOn(game,BoardLayout.FieldAt(game,BoardLayout.SizeIndex(boardSize),at));
        if(piece>=0&&game.LegalMoves().Contains(piece))await PlayMove(piece);
    }
    private static int NextSelectable(int[] moves,int start)
    {
        for(int k=0;k<4;k++)if(moves.Contains((start+k)%4))return (start+k)%4;
        return moves[0];
    }
    internal int TestSelectionIndex => selectionIndex;
    internal int TestShownDie => shownDie;
    internal static Point TitleOrigin(int boardSize)=>new((PlayerPanelLayout.ForBoard(boardSize).Bounds.Right+2-boardSize)/2,2);
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if(automated) {TestKeyCount++;TestLastKey=keyData;}
        if(keyData==Keys.F1&&!busy&&!modal&&!closingApproved) {HelpDialog();return true;}
        if ((busy||closingApproved) && keyData is Keys.Space or Keys.Enter or Keys.Tab) return true;
        if(playing&&!modal&&!sizingWindow&&!game.Finished&&game.Kinds[game.Player]==PlayerKind.Human&&(keyData==Keys.Space||keyData==Keys.Enter||keyData==Keys.Tab))
        {
            if(!game.AwaitingMove) {if(keyData!=Keys.Tab)_ = Roll();return true;}
            var moves=game.LegalMoves();
            if(moves.Length==0)return true;
            // seg09:0b65: the first Tab/Enter/Space shows the remembered piece or the next movable one
            // after it; further Tabs advance cyclically (seg04:0412), Enter/Space moves the shown piece.
            if(selected<0)selected=selectionIndex=NextSelectable(moves,selectionIndex);
            else if(keyData==Keys.Tab)selected=selectionIndex=NextSelectable(moves,(selected+1)%4);
            else if(moves.Contains(selected))_ = PlayMove(selected);
            surface.Invalidate();return true;
        }
        return base.ProcessCmdKey(ref msg,keyData);
    }
    private void RefreshGame()
    {
        // Tests must fail visibly; an exception here could be lost in a discarded task.
        if(automated&&InvokeRequired)Environment.FailFast("Spielzustand wird außerhalb des UI-Threads aktualisiert.");
        // Ein beim Spielerwechsel übernommener Brettwechsel ändert das Brett der Partie.
        if(playing)boardPlayers=game.PlayerCount;
        // seg07:042c: ein vorgemerkter Wechsel aufs Viererbrett scheitert erst beim Spielerwechsel.
        if(playing&&!modal&&!busy&&game.TakeBoardChangeRejected()){ShowOriginal("DLG_KEINUMSCHALTEN");return;}
        // A change applied at the transition can leave no player for the turn (e.g. six to four
        // with only finished or inactive players 1..4). The original loops forever in
        // seg04:0332..0373; here the game ends with the ranking.
        if(playing&&!modal&&!busy&&game.Finished&&game.Winners.Count==announcedWinners){EndGame();ShowWinners();return;}
        if(playing&&!busy&&!modal)
        {
            int previousPlayer=game.Player;
            bool pendingMove=game.AwaitingMove;
            game.ReconcileTurn();
            if(pendingMove&&!game.AwaitingMove)repeatRollDelay=!game.Finished&&game.Player==previousPlayer;
            if(previousPlayer!=game.Player||!game.LegalMoves().Contains(selected))selected=-1;
        }
        if(nativeMenu!=0) {WindowChrome.EnableMenuItem(nativeMenu,202,playing?0u:1u);WindowChrome.EnableMenuItem(nativeMenu,101,playing?0u:1u);}
        // Original DS:010c: initial-roll, retry-roll and move delays for each speed.
        int[] delays=game.AwaitingMove?[2000,600,200,0]:repeatRollDelay?[1000,500,200,0]:[1200,600,100,0];
        timer.Stop();computerWake?.Cancel();timer.Interval=delays[computerSpeed]+30;
        if(!closingApproved){timer.Start();WakeComputer(delays[computerSpeed]);}
        UpdateCursor();
        surface.Invalidate();
    }
    internal void PrepareInteractionTest()
    {
        for(int p=0;p<6;p++)game.Kinds[p]=PlayerKind.Human;
        NewGame();
    }
    internal Game TestState => game;
    internal bool TestBusy => busy;
    internal bool TestModal => modal;
    internal GameSound TestSound => sound;
    internal Bitmap TestBoardImage()
    {
        var picture=new Bitmap(surface.Width,surface.Height);
        surface.DrawToBitmap(picture,new Rectangle(Point.Empty,surface.Size));
        return picture;
    }
    internal void TestStopGame()=>playing=false;
    internal bool TestPlaying=>playing;
    internal void LoadState(Game loaded)
    {
        game=loaded;boardPlayers=game.PlayerCount;playing=true;selected=-1;
        // seg09:0202: after loading the die shows only with a pending move or failed attempts.
        announcedWinners=game.Winners.Count;shownDie=game.AwaitingMove||game.Attempts>0?game.Die:0;repeatRollDelay=false;
        RefreshGame();
    }
    internal void TestLoadExample()
    {
        var loaded=OriginalSave.Read(File.ReadAllBytes(Path.Combine(directory,"BEISPIEL.ERG")));
        for(int p=0;p<6;p++)loaded.Kinds[p]=PlayerKind.Human;
        LoadState(loaded);
        File.WriteAllBytes(TestOutput.PathFor("compatibility-test.erg"),OriginalSave.Write(game));
    }
    private void PaintBoard(object? sender,PaintEventArgs e)
    {
        int sizeIndex=boardSize==200?0:boardSize==400?1:2;
        if(!playing)
        {
            // seg17:00c7 first blanks the player panel and die areas (DS:1648/1684) white,
            // a no-op on the white background, then draws the title at
            // x = (panel right + 2 - board size) / 2 (DS:164c, DS:16cc), y = 2.
            var title=Image("title",sizeIndex);
            e.Graphics.DrawImageUnscaled(title,TitleOrigin(boardSize));return;
        }
        bool six=game.PlayerCount==6;
        // seg17:0291: board bitmap at (2,2); with the rest rule the grey rest field image
        // (DS:204e) at each rest field origin + 2.
        e.Graphics.DrawImageUnscaled(Image("board",sizeIndex*2+(six?1:0)),BoardLayout.Margin,BoardLayout.Margin);
        int figureBase=sizeIndex*20+(six?8:0);
        int ornamentBase=sizeIndex*32;
        var track=BoardLayout.Track(six,sizeIndex);
        if(game.Rules.Rest)
        {
            var rest=Image("extra",ornamentBase+(six?30:15));
            for(int p=0;p<game.PlayerCount;p++)
            {var at=track[p*(game.TrackLength/game.PlayerCount)+game.RestOffset];e.Graphics.DrawImageUnscaled(rest,at.X+BoardLayout.Margin,at.Y+BoardLayout.Margin);}
        }
        for(int p=0;p<game.PlayerCount;p++)
            if(game.Kinds[p]!=PlayerKind.Inactive)
                for(int i=0;i<4;i++)
                {
                    if(p==movingPlayer&&i==movingPiece)continue;
                    if(hiddenCapturedPieces.Contains((p,i)))
                    {
                        // seg04:13da/0e3e: the victim shows its sad figure on its field
                        // until the attacking piece is drawn over it.
                        if(movingPosition!=movingTarget)
                            e.Graphics.DrawImageUnscaled(Image("figure",figureBase+p*2+1),BoardLayout.PieceAt(game,sizeIndex,p,i));
                        continue;
                    }
                    DrawPiece(p,i,game.Position(p,i));
                }
        // seg05:018a/020c: each animation step redraws the left field and then draws the
        // moving piece with seg17:04c1, so it lies above any piece it passes.
        if(movingPlayer>=0)DrawPiece(movingPlayer,movingPiece,movingPosition);
        void DrawPiece(int p,int i,int position)
        {
            var at=BoardLayout.PieceAt(game,sizeIndex,p,i,position);
            // seg17:04c1: yard pieces use the second (sad) figure (DS:1d7e), others the first (DS:1d7a).
            e.Graphics.DrawImageUnscaled(Image("figure",figureBase+p*2+(position<0?1:0)),at.X,at.Y);
            // seg17:06b1..0840: on a rest field the four corner marks (DS:2066) are redrawn over the piece.
            if(game.Rules.Rest&&position>=0&&position<game.TrackLength&&game.IsRestSquare(game.Square(p,position)))
            {
                var corner=Image("extra",ornamentBase+(six?31:16));
                var field=track[game.Square(p,position)];
                foreach(var offset in BoardLayout.RestCorners(six,sizeIndex))
                    e.Graphics.DrawImageUnscaled(corner,field.X+offset.X+BoardLayout.Margin,field.Y+offset.Y+BoardLayout.Margin);
            }
        }
        // seg09:0223 -> seg17:1558: after the pieces a black 1-pixel square of the field size,
        // starting at the piece's drawing position (field origin + 2).
        if(selected>=0&&game.AwaitingMove)
        {
            var at=BoardLayout.PieceAt(game,sizeIndex,game.Player,selected);
            int size=BoardLayout.FieldSize(six,sizeIndex);
            e.Graphics.DrawRectangle(Pens.Black,at.X,at.Y,size-1,size-1);
        }
        var panel=PlayerPanelLayout.ForBoard(boardSize);
        using var fill=new SolidBrush(Color.FromArgb(255,255,75));
        e.Graphics.FillRectangle(fill,panel.Bounds);
        e.Graphics.DrawRectangle(Pens.Red,panel.Bounds.X,panel.Bounds.Y,panel.Bounds.Width-1,panel.Bounds.Height-1);
        var figureAt=six?panel.SixPlayerFigure:panel.FourPlayerFigure;
        e.Graphics.DrawImageUnscaled(Image("figure",figureBase+game.Player*2),figureAt);
        if(shownDie>0)e.Graphics.DrawImageUnscaled(Image("extra",ornamentBase+shownDie-1),panel.Die);
        using var font=new Font("Arial",panel.FontPixels,FontStyle.Bold,GraphicsUnit.Pixel);
        int nameHalfWidth=Math.Min(panel.Name.X-panel.Bounds.Left-2,panel.Bounds.Right-panel.Name.X-2);
        var nameBounds=new Rectangle(panel.Name.X-nameHalfWidth,panel.Name.Y,nameHalfWidth*2,panel.Bounds.Bottom-panel.Name.Y-2);
        TextRenderer.DrawText(e.Graphics,game.Names[game.Player],font,nameBounds,Color.Black,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
        using var copyright=new Font("Times New Roman",panel.CopyrightFontPixels,FontStyle.Regular,GraphicsUnit.Pixel);
        string[] copyrightLines=["1995","by","Oliver","Kraheck"];
        for(int line=0;line<copyrightLines.Length;line++)
        {
            var bounds=new Rectangle(panel.Copyright.X-panel.Bounds.Width/2,panel.Copyright.Y+line*panel.CopyrightLineHeight,
                panel.Bounds.Width,panel.CopyrightLineHeight);
            TextRenderer.DrawText(e.Graphics,copyrightLines[line],copyright,bounds,Color.Black,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        }
    }
    private void LoadSettings()
    {
        // Fehlende, beschädigte oder gesperrte AERGER.CFG verhindert den Start nicht: Standardwerte bleiben,
        // „Einstellungen speichern“ schreibt die Datei neu.
        if(SettingsFile.TryRead(Path.Combine(directory,"AERGER.CFG")) is not { } b)return;
        game.Rules=new(b[0]!=0,b[1]!=0,b[2]!=0,b[3]!=0,b[4]!=0,b[5]!=0,b[6]!=0);
        speech=b[7]!=0;effects=b[8]!=0;humanSpeed=b[9];computerSpeed=b[10];boardSize=new[]{200,400,700}[b[11]-1];boardPlayers=b[12]==0?4:6;
    }
    private void SaveSettings()
    {
        var r=game.Rules;
        byte[] data=[(byte)(r.Rest?1:0),(byte)(r.Capture?1:0),(byte)(r.Hard?1:0),(byte)(r.Absolute?1:0),(byte)(r.NoJump?1:0),(byte)(r.EnterOnOne?1:0),(byte)(r.RepeatOnOne?1:0),(byte)(speech?1:0),(byte)(effects?1:0),(byte)humanSpeed,(byte)computerSpeed,(byte)(boardSize==200?1:boardSize==400?2:3),(byte)(boardPlayers==4?0:1)];
        try {File.WriteAllBytes(Path.Combine(directory,"AERGER.CFG"),data);}
        catch(IOException ex){MessageBox.Show(this,ex.Message,Text);}
        catch(UnauthorizedAccessException ex){MessageBox.Show(this,ex.Message,Text);}
    }
    protected override void Dispose(bool disposing)
    {if(disposing){lifetime.Cancel();timer.Dispose();sound.Dispose();foreach(var bitmap in images.Values)bitmap.Dispose();mouseCursor.Dispose();diceCursor.Dispose();figureCursor.Dispose();}base.Dispose(disposing);}
    private sealed class BoardPanel:Panel
    {public BoardPanel(){DoubleBuffered=true;BackColor=Color.White;}}
}
