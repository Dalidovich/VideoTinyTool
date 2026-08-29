using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VideoTinyTool.Platform;

[SupportedOSPlatform("windows")]
public static class ConsoleWindow
{
    private const int SwHide = 0;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void Hide()
    {
        var handle = GetConsoleWindow();
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SwHide);
        }
    }
}
