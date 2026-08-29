using System.Runtime.Versioning;
using VideoTinyTool.Application;
using VideoTinyTool.Platform;

namespace VideoTinyTool;

[SupportedOSPlatform("windows")]
internal class Program
{
    private static void Main(string[] args)
    {
        NativeLibraries.Deploy();
        ConsoleWindow.Hide();

        var settings = SettingsLoader.LoadOrCreate(AppPaths.SettingsFile);

        using var application = new EditorApplication(settings);
        application.Run();
    }
}
