namespace Aerger;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            bool automated=args.Contains("--smoke-test")||args.Contains("--ui-test")||args.Contains("--dialog-test")||args.Contains("--help-test")||args.Contains("--timeline-test");
            if(automated&&!args.Contains(AutomationDesktop.ChildArgument))
            {Environment.ExitCode=AutomationDesktop.Run(args);return;}
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetDefaultFont(new Font("Microsoft Sans Serif",8.25f));
            WindowChrome.SetThemeAppProperties(0);
            // The original polls GetTickCount in its idle loop (seg01:0079, seg05:0022/0232), so its
            // 10..65 ms animation delays hold to the millisecond. Ask for 1 ms timer resolution so
            // Task.Delay and WM_TIMER do not round them up to the default 15.6 ms tick.
            TimerResolution.Begin();
            if(automated)
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                // The tests drive the window without Application.Run. WinForms then removes its
                // synchronization context when a nested message loop ends, so later awaits in
                // animations would resume on thread-pool threads. Keep one fixed UI context.
                WindowsFormsSynchronizationContext.AutoInstall=false;
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            }
            using var window = new GameWindow(automated);
            if (automated)
            {
                window.Show();
                Application.DoEvents();
                if(args.Contains("--ui-test")) InteractionTest.Run(window);
                else if(args.Contains("--timeline-test")) InteractionTest.RunTimelines(window);
                else if(args.Contains("--dialog-test"))
                {window.PrepareInteractionTest();DialogInteractionTest.Run(window);}
                else if(args.Contains("--help-test"))
                {
                    var pending=new Game();pending.Roll(6);window.LoadState(pending);
                    InteractionTest.RunHelp(window);
                }
                using var screenshot = new Bitmap(window.Width, window.Height);
                window.DrawToBitmap(screenshot, new Rectangle(Point.Empty, window.Size));
                screenshot.Save(TestOutput.PathFor("smoke-test.png"));
                if(args.Contains("--ui-test")||args.Contains("--dialog-test")||args.Contains("--help-test")||args.Contains("--timeline-test"))
                {
                    window.TestStopGame();window.Close();
                    long deadline=Environment.TickCount64+10000;
                    while(!window.IsDisposed&&Environment.TickCount64<deadline){Application.DoEvents();Thread.Sleep(5);}
                    if(!window.IsDisposed)throw new Exception("Fenster wird beim Beenden nicht geschlossen.");
                    if(args.Contains("--ui-test"))File.AppendAllText(TestOutput.PathFor("ui-test.txt"),"PASS: animated roll/move, original save loading, BWCC rendering, asynchronous close.\n");
                }
                return;
            }
            Application.Run(window);
        }
        catch (Exception ex)
        {
            if (args.Contains("--smoke-test")||args.Contains("--ui-test")||args.Contains("--dialog-test")||args.Contains("--help-test")||args.Contains("--timeline-test")) throw;
            MessageBox.Show("Ärger konnte nicht gestartet werden.\n\n" + ex.Message, "Ärger", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
