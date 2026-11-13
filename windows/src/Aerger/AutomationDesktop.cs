using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Aerger;

// Inherit the private desktop at process creation: the CLR can create an STA
// helper window before Main, making SetThreadDesktop too late.
internal static class AutomationDesktop
{
    internal const string ChildArgument = "--automation-desktop-child";
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public nint Reserved;
        public string? Desktop;
        public nint Title;
        public uint X, Y, Width, Height, Columns, Rows, Fill, Flags;
        public ushort Show, ReservedSize;
        public nint ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        public nint Process, Thread;
        public uint ProcessId, ThreadId;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateDesktopW(string name, nint device, nint mode,
        uint flags, uint access, nint security);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(nint desktop);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string application, StringBuilder commandLine, nint processSecurity,
        nint threadSecurity, bool inheritHandles, uint flags, nint environment, string? directory,
        ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] private static extern nint GetStdHandle(int kind);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(nint process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(nint process, uint code);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);

    // Windows command-line quoting, including quotes and trailing backslashes.
    private static string Quote(string value)
    {
        var result=new StringBuilder("\"");
        int slashes=0;
        foreach(char c in value)
        {
            if(c=='\\'){slashes++;continue;}
            result.Append('\\',c=='"'?slashes*2+1:slashes).Append(c);
            slashes=0;
        }
        return result.Append('\\',slashes*2).Append('"').ToString();
    }

    internal static int Run(string[] arguments)
    {
        string name=$"AergerTests-{Environment.ProcessId}-{Guid.NewGuid():N}";
        // Desktop rights except SWITCHDESKTOP. Never display this desktop.
        nint desktop=CreateDesktopW(name,0,0,0,0x00ff,0);
        if(desktop==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"Testdesktop konnte nicht angelegt werden.");
        ProcessInfo child=default;
        try
        {
            string executable=Environment.ProcessPath??throw new InvalidOperationException("Programmpfad fehlt.");
            var command=new StringBuilder(string.Join(" ",new[]{executable}.Concat(arguments).Append(ChildArgument).Select(Quote)));
            var startup=new StartupInfo {Size=Marshal.SizeOf<StartupInfo>(),Desktop=name,Flags=0x100,
                Input=GetStdHandle(-10),Output=GetStdHandle(-11),Error=GetStdHandle(-12)};
            if(!CreateProcessW(executable,command,0,0,true,0,0,null,ref startup,out child))
                throw new Win32Exception(Marshal.GetLastWin32Error(),"Testprozess konnte nicht gestartet werden.");
            uint wait=WaitForSingleObject(child.Process,240_000);
            if(wait!=0)
            {
                if(TerminateProcess(child.Process,1))WaitForSingleObject(child.Process,5_000);
                throw new InvalidOperationException($"Testprozess nicht rechtzeitig beendet (Warteergebnis {wait:X}).");
            }
            if(!GetExitCodeProcess(child.Process,out uint code))throw new Win32Exception(Marshal.GetLastWin32Error());
            return unchecked((int)code);
        }
        finally
        {
            if(child.Thread!=0)CloseHandle(child.Thread);
            if(child.Process!=0)CloseHandle(child.Process);
            CloseDesktop(desktop);
        }
    }
}
