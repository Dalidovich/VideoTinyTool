using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VideoTinyTool.Application;

namespace VideoTinyTool.Platform;

[SupportedOSPlatform("windows")]
public static class NativeLibraries
{
    private const string ResourcePrefix = "native/";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    public static void Deploy()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToArray();

        if (resources.Length == 0)
        {
            return;
        }

        var directory = AppPaths.NativeDirectory;
        Directory.CreateDirectory(directory);

        foreach (var resource in resources)
        {
            using var source = assembly.GetManifestResourceStream(resource);
            if (source is null)
            {
                continue;
            }

            var path = Path.Combine(directory, resource[ResourcePrefix.Length..]);
            var existing = new FileInfo(path);
            if (existing.Exists && existing.Length == source.Length)
            {
                continue;
            }

            Extract(source, path);
        }

        SetDllDirectory(directory);
    }

    private static void Extract(Stream source, string path)
    {
        var staging = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            using (var destination = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(destination);
            }

            File.Move(staging, path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && File.Exists(path))
        {
            TryDelete(staging);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
