using SFML.Graphics;
using VideoTinyTool.Application;

namespace VideoTinyTool.Ui;

public sealed class FontCatalog : IDisposable
{
    private static readonly string[] SystemFallbacks =
    [
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        @"C:\Windows\Fonts\tahoma.ttf"
    ];

    private static readonly string[] SystemMonoFallbacks =
    [
        @"C:\Windows\Fonts\consola.ttf",
        @"C:\Windows\Fonts\cour.ttf"
    ];

    private FontCatalog(Font regular, Font semiBold, Font mono, string? warning)
    {
        Regular = regular;
        SemiBold = semiBold;
        Mono = mono;
        Warning = warning;
    }

    public Font Regular { get; }

    public Font SemiBold { get; }

    public Font Mono { get; }

    public string? Warning { get; }

    public static FontCatalog Load()
    {
        var missing = new List<string>();

        var regular = LoadFont("Inter-Regular.ttf", SystemFallbacks, missing);
        var semiBold = LoadFont("Inter-SemiBold.ttf", SystemFallbacks, missing)
                       ?? regular;
        var mono = LoadFont("JetBrainsMono-Regular.ttf", SystemMonoFallbacks, missing)
                   ?? regular;

        if (regular is null)
        {
            throw new InvalidOperationException(
                "No usable font was found. Expected the fonts folder next to the executable.");
        }

        var warning = missing.Count == 0
            ? null
            : "These font files are missing next to the executable, system fonts are used instead:\n"
              + string.Join("\n", missing);

        return new FontCatalog(regular, semiBold!, mono!, warning);
    }

    private static Font? LoadFont(string fileName, IReadOnlyList<string> fallbacks, List<string> missing)
    {
        var path = AppPaths.Font(fileName);
        if (File.Exists(path))
        {
            try
            {
                return new Font(path);
            }
            catch (Exception)
            {
                // Falls through to the system fonts below.
            }
        }

        missing.Add(path);

        foreach (var fallback in fallbacks)
        {
            if (!File.Exists(fallback))
            {
                continue;
            }

            try
            {
                return new Font(fallback);
            }
            catch (Exception)
            {
                // Tries the next candidate.
            }
        }

        return null;
    }

    public void Dispose()
    {
        var disposed = new List<Font>();
        foreach (var font in new[] { Mono, SemiBold, Regular })
        {
            if (disposed.Any(other => ReferenceEquals(other, font)))
            {
                continue;
            }

            disposed.Add(font);
            font.Dispose();
        }
    }
}
