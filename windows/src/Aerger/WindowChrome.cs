using System.Runtime.InteropServices;

namespace Aerger;

internal static class WindowChrome
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] internal static extern int GetWindowLongW(nint window,int index);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint window,out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool AdjustWindowRectEx(ref Rect rectangle,uint style,bool menu,uint extendedStyle);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    internal static void ResizeClient(nint window,int width,int height)
    {
        var size=OuterForClient(window,new(width,height));
        SetWindowPos(window,0,0,0,size.Width,size.Height,0x16);
    }
    internal static Size OuterForClient(nint window,Size client)
    {
        var rectangle=new Rect {Right=client.Width,Bottom=client.Height};
        AdjustWindowRectEx(ref rectangle,(uint)GetWindowLongW(window,-16),true,(uint)GetWindowLongW(window,-20));
        return new(rectangle.Right-rectangle.Left,rectangle.Bottom-rectangle.Top);
    }
    [DllImport("user32.dll")] internal static extern nint CreateMenu();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern bool AppendMenuW(nint menu,uint flags,nuint id,string? text);
    [DllImport("user32.dll")] internal static extern bool SetMenu(nint window,nint menu);
    [DllImport("user32.dll")] internal static extern bool DrawMenuBar(nint window);
    [DllImport("user32.dll")] internal static extern uint EnableMenuItem(nint menu,uint id,uint flags);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] internal static extern int SetWindowTheme(nint window,string app,string id);
    [DllImport("uxtheme.dll")] internal static extern void SetThemeAppProperties(uint flags);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window,int attribute,ref int value,int size);
    internal static void OriginalFrame(nint window)
    {
        SetWindowTheme(window,"","");
        SetWindowPos(window,0,0,0,0,0,0x37);
    }
}
