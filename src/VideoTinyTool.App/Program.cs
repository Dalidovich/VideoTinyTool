using System.Runtime.Versioning;
using VideoTinyTool.Application;
using VideoTinyTool.Localization;
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
        I18n.Use(LocalizationCatalog.Load(settings.Ui.Language));

        using var application = new EditorApplication(settings);
        application.Run();
    }
}
