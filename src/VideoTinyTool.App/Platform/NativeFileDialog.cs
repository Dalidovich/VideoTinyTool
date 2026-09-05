using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VideoTinyTool.Localization;

namespace VideoTinyTool.Platform;

[SupportedOSPlatform("windows")]
public static class NativeFileDialog
{
    private const int MaxMultiSelectBuffer = 64 * 1024;

    private const string VideoPatterns = "*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.mpg;*.mpeg;*.wmv;*.ts;*.mts;*.m2ts;*.flv";
    private const string AudioPatterns = "*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma";

    private const int OfnReadOnly = 0x00000001;
    private const int OfnHideReadOnly = 0x00000004;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnAllowMultiSelect = 0x00000200;
    private const int OfnExplorer = 0x00080000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnOverwritePrompt = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string? lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileNameW(ref OpenFileName ofn);

    public static IReadOnlyList<string> OpenMediaFiles(IntPtr owner)
    {
        var buffer = Marshal.AllocHGlobal(MaxMultiSelectBuffer * sizeof(char));
        try
        {
            for (var i = 0; i < 4; i++)
            {
                Marshal.WriteInt16(buffer, i * sizeof(char), 0);
            }

            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = BuildFilter(
                    (I18n.FileDialogs.MediaFiles, $"{VideoPatterns};{AudioPatterns}"),
                    (I18n.FileDialogs.VideoFiles, VideoPatterns),
                    (I18n.FileDialogs.AudioFiles, AudioPatterns),
                    (I18n.FileDialogs.AllFiles, "*.*")),
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = MaxMultiSelectBuffer,
                lpstrTitle = I18n.FileDialogs.ImportTitle,
                Flags = OfnExplorer | OfnAllowMultiSelect | OfnFileMustExist | OfnPathMustExist
                        | OfnHideReadOnly | OfnNoChangeDir | OfnReadOnly
            };

            return GetOpenFileNameW(ref ofn) ? ReadMultiSelect(buffer) : Array.Empty<string>();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string? SaveFile(
        IntPtr owner,
        string container,
        string suggestedName,
        string filterLabel,
        string? title = null)
    {
        var buffer = Marshal.AllocHGlobal(1024 * sizeof(char));
        try
        {
            var initial = suggestedName.AsSpan(0, Math.Min(suggestedName.Length, 500)).ToString();
            var bytes = new char[512];
            initial.CopyTo(bytes);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);

            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = BuildFilter((filterLabel, $"*.{container}"), (I18n.FileDialogs.AllFiles, "*.*")),
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = 1024,
                lpstrTitle = title ?? I18n.FileDialogs.ExportTitle,
                lpstrDefExt = container,
                Flags = OfnExplorer | OfnOverwritePrompt | OfnPathMustExist | OfnHideReadOnly | OfnNoChangeDir
            };

            if (!GetSaveFileNameW(ref ofn))
            {
                return null;
            }

            var path = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string BuildFilter(params (string Label, string Pattern)[] entries)
    {
        var text = string.Empty;
        foreach (var (label, pattern) in entries)
        {
            text += $"{label} ({pattern})\0{pattern}\0";
        }

        return text + "\0";
    }

    private static IReadOnlyList<string> ReadMultiSelect(IntPtr buffer)
    {
        var parts = new List<string>();
        var offset = 0;
        while (offset < MaxMultiSelectBuffer)
        {
            var current = Marshal.PtrToStringUni(buffer + (offset * sizeof(char)));
            if (string.IsNullOrEmpty(current))
            {
                break;
            }

            parts.Add(current);
            offset += current.Length + 1;
        }

        if (parts.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (parts.Count == 1)
        {
            return parts;
        }

        var directory = parts[0];
        var files = new List<string>(parts.Count - 1);
        for (var i = 1; i < parts.Count; i++)
        {
            files.Add(Path.Combine(directory, parts[i]));
        }

        return files;
    }
}
