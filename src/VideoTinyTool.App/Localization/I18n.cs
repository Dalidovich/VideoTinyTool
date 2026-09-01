using System.Globalization;

namespace VideoTinyTool.Localization;

public static class I18n
{
    private static LocalizationCatalog _catalog = LocalizationCatalog.BuiltIn();

    public static string Language => _catalog.Language;

    public static string? Warning => _catalog.Warning;

    public static void Use(LocalizationCatalog catalog) => _catalog = catalog;

    public static class Brand
    {
        public static string Head => _catalog.Text("brand.head");
        public static string Accent => _catalog.Text("brand.accent");
        public static string Tail => _catalog.Text("brand.tail");
        public static string Full => Head + Accent + Tail;
    }

    public static class Toolbar
    {
        public static string Import => _catalog.Text("toolbar.import");
        public static string Split => _catalog.Text("toolbar.split");
        public static string Remove => _catalog.Text("toolbar.remove");
        public static string Undo => _catalog.Text("toolbar.undo");
        public static string Redo => _catalog.Text("toolbar.redo");
        public static string Export => _catalog.Text("toolbar.export");
        public static string Help => _catalog.Text("toolbar.help");
    }

    public static class Help
    {
        public static string Title => _catalog.Text("help.title");

        public static string Playback => _catalog.Text("help.playback");
        public static string Editing => _catalog.Text("help.editing");
        public static string Files => _catalog.Text("help.files");
        public static string View => _catalog.Text("help.view");
        public static string Dialogs => _catalog.Text("help.dialogs");

        public static string PlayPause => _catalog.Text("help.playPause");
        public static string Shuttle => _catalog.Text("help.shuttle");
        public static string StepFrame => _catalog.Text("help.stepFrame");
        public static string StepSecond => _catalog.Text("help.stepSecond");
        public static string JumpToEnds => _catalog.Text("help.jumpToEnds");

        public static string Split => _catalog.Text("help.split");
        public static string Trim => _catalog.Text("help.trim");
        public static string Remove => _catalog.Text("help.remove");
        public static string RippleDelete => _catalog.Text("help.rippleDelete");
        public static string Undo => _catalog.Text("help.undo");
        public static string Redo => _catalog.Text("help.redo");

        public static string Import => _catalog.Text("help.import");
        public static string Export => _catalog.Text("help.export");

        public static string Zoom => _catalog.Text("help.zoom");
        public static string ZoomAtCursor => _catalog.Text("help.zoomAtCursor");
        public static string ScrollTimeline => _catalog.Text("help.scrollTimeline");
        public static string ShowHelp => _catalog.Text("help.showHelp");
        public static string AddTrack => _catalog.Text("help.addTrack");
        public static string RemoveTrack => _catalog.Text("help.removeTrack");

        public static string Confirm => _catalog.Text("help.confirm");
        public static string Dismiss => _catalog.Text("help.dismiss");
    }

    public static class Preview
    {
        public static string Title => _catalog.Text("preview.title");
        public static string EmptyTimeline => _catalog.Text("preview.emptyTimeline");
        public static string PreparingFrame => _catalog.Text("preview.preparingFrame");

        public static string Format(int width, int height, int frameRate) =>
            _catalog.Format("preview.format", width, height, frameRate);

        public static string RateBadge(double rate) =>
            _catalog.Format("preview.rateBadge", rate.ToString("0.#", CultureInfo.InvariantCulture));

        public static string OverlayHint => _catalog.Text("preview.overlayHint");
        public static string OverlayOpacity => _catalog.Text("preview.overlayOpacity");
        public static string OverlayVolume => _catalog.Text("preview.overlayVolume");
        public static string OverlayReset => _catalog.Text("preview.overlayReset");

        public static string OverlayPercent(float value) =>
            _catalog.Format("preview.overlayPercent", (int)MathF.Round(value * 100f));
    }

    public static class Sources
    {
        public static string Title => _catalog.Text("sources.title");
        public static string Empty => _catalog.Text("sources.empty");
        public static string FileMissing => _catalog.Text("sources.fileMissing");
        public static string NoAudio => _catalog.Text("sources.noAudio");
        public static string AudioOnly => _catalog.Text("sources.audioOnly");

        public static string FileCount(int count) => _catalog.Plural("sources.fileCount", count, count);

        public static string Meta(string duration, int width, int height, string frameRate) =>
            _catalog.Format("sources.meta", duration, width, height, frameRate);

        public static string AudioMeta(string duration, string codec) =>
            _catalog.Format("sources.audioMeta", duration, codec);
    }

    public static class Timeline
    {
        public static string Title => _catalog.Text("timeline.title");
        public static string MissingSource => _catalog.Text("timeline.missingSource");
        public static string InPrefix => _catalog.Text("timeline.inPrefix");
        public static string OutPrefix => _catalog.Text("timeline.outPrefix");
        public static string NoTimecode => _catalog.Text("timeline.noTimecode");
        public static string Zoom => _catalog.Text("timeline.zoom");
        public static string AddTrack => _catalog.Text("timeline.addTrack");
        public static string RemoveTrack => _catalog.Text("timeline.removeTrack");

        public static string TrackLabel(int number) => _catalog.Format("timeline.trackLabel", number);

        public static string ClipCount(int count) => _catalog.Plural("timeline.clipCount", count, count);
    }

    public static class ExportSetup
    {
        public static string Title => _catalog.Text("exportSetup.title");
        public static string Start => _catalog.Text("exportSetup.start");

        public static string Container => _catalog.Text("exportSetup.container");
        public static string ContainerHint => _catalog.Text("exportSetup.containerHint");

        public static string VideoCodec => _catalog.Text("exportSetup.videoCodec");
        public static string VideoCodecHint => _catalog.Text("exportSetup.videoCodecHint");

        public static string Quality => _catalog.Text("exportSetup.quality");
        public static string QualityHint => _catalog.Text("exportSetup.qualityHint");

        public static string Preset => _catalog.Text("exportSetup.preset");
        public static string PresetHint => _catalog.Text("exportSetup.presetHint");

        public static string Resolution => _catalog.Text("exportSetup.resolution");
        public static string ResolutionHint => _catalog.Text("exportSetup.resolutionHint");

        public static string FrameRate => _catalog.Text("exportSetup.frameRate");
        public static string FrameRateHint => _catalog.Text("exportSetup.frameRateHint");

        public static string Speed => _catalog.Text("exportSetup.speed");
        public static string SpeedHint => _catalog.Text("exportSetup.speedHint");

        public static string AudioCodec => _catalog.Text("exportSetup.audioCodec");
        public static string AudioCodecHint => _catalog.Text("exportSetup.audioCodecHint");

        public static string AudioBitrate => _catalog.Text("exportSetup.audioBitrate");
        public static string AudioBitrateHint => _catalog.Text("exportSetup.audioBitrateHint");
    }

    public static class Dialogs
    {
        public static string Ok => _catalog.Text("dialogs.ok");
        public static string Cancel => _catalog.Text("dialogs.cancel");

        public static string RemoveSourceTitle => _catalog.Text("dialogs.removeSourceTitle");
        public static string RemoveSourceConfirm => _catalog.Text("dialogs.removeSourceConfirm");

        public static string FFmpegMissingTitle => _catalog.Text("dialogs.ffmpegMissingTitle");

        public static string MissingSourcesTitle => _catalog.Text("dialogs.missingSourcesTitle");

        public static string ExportingTitle => _catalog.Text("dialogs.exportingTitle");
        public static string ExportFinishedTitle => _catalog.Text("dialogs.exportFinishedTitle");
        public static string ExportCancelledTitle => _catalog.Text("dialogs.exportCancelledTitle");
        public static string ExportCancelledMessage => _catalog.Text("dialogs.exportCancelledMessage");
        public static string ExportFailedTitle => _catalog.Text("dialogs.exportFailedTitle");
        public static string ExportFailedFallback => _catalog.Text("dialogs.exportFailedFallback");

        public static string ExportRunningTitle => _catalog.Text("dialogs.exportRunningTitle");
        public static string ExportRunningMessage => _catalog.Text("dialogs.exportRunningMessage");
        public static string StopAndClose => _catalog.Text("dialogs.stopAndClose");
        public static string KeepExporting => _catalog.Text("dialogs.keepExporting");

        public static string SettingsTitle => _catalog.Text("dialogs.settingsTitle");
        public static string FontsTitle => _catalog.Text("dialogs.fontsTitle");
        public static string LocalizationTitle => _catalog.Text("dialogs.localizationTitle");

        public static string Percent(double fraction) =>
            _catalog.Format("dialogs.percent", (fraction * 100).ToString("0", CultureInfo.InvariantCulture));

        public static string RemoveSourceMessage(string fileName, int usage) =>
            _catalog.Plural("dialogs.removeSourceMessage", usage, fileName, usage);

        public static string ImportFailedTitle(int count) =>
            _catalog.Plural("dialogs.importFailedTitle", count);

        public static string ImportFailure(string fileName, string reason) =>
            _catalog.Format("dialogs.importFailure", fileName, reason);

        public static string MissingSourcesMessage(IEnumerable<string> paths) =>
            _catalog.Format("dialogs.missingSourcesMessage", string.Join("\n", paths));
    }

    public static class FileDialogs
    {
        public static string ImportTitle => _catalog.Text("fileDialogs.importTitle");
        public static string ExportTitle => _catalog.Text("fileDialogs.exportTitle");
        public static string MediaFiles => _catalog.Text("fileDialogs.mediaFiles");
        public static string VideoFiles => _catalog.Text("fileDialogs.videoFiles");
        public static string AudioFiles => _catalog.Text("fileDialogs.audioFiles");
        public static string AllFiles => _catalog.Text("fileDialogs.allFiles");

        public static string ContainerVideo(string container) =>
            _catalog.Format("fileDialogs.containerVideo", container.ToUpperInvariant());

        public static string DefaultExportName(string container) =>
            _catalog.Format("fileDialogs.defaultExportName", container);
    }

    public static class Startup
    {
        public static string NoUsableFont => _catalog.Text("startup.noUsableFont");

        public static string MissingBinaries(IEnumerable<string> paths) =>
            _catalog.Format("startup.missingBinaries", string.Join("\n", paths));

        public static string MissingFonts(IEnumerable<string> files) =>
            _catalog.Format("startup.missingFonts", string.Join("\n", files));

        public static string SettingsUnreadable(string reason) =>
            _catalog.Format("startup.settingsUnreadable", reason);

        public static string SettingsNotCreated(string reason) =>
            _catalog.Format("startup.settingsNotCreated", reason);
    }

    public static class Probe
    {
        public static string FileNotFound => _catalog.Text("probe.fileNotFound");
        public static string FFprobeNotStarted => _catalog.Text("probe.ffprobeNotStarted");
        public static string NoStreams => _catalog.Text("probe.noStreams");
        public static string NoPlayableStream => _catalog.Text("probe.noPlayableStream");
        public static string NoFrameSize => _catalog.Text("probe.noFrameSize");
        public static string NoDuration => _catalog.Text("probe.noDuration");

        public static string FFprobeNotFound(string path) => _catalog.Format("probe.ffprobeNotFound", path);

        public static string FFprobeExited(int exitCode) => _catalog.Format("probe.ffprobeExited", exitCode);

        public static string UnreadableOutput(string reason) => _catalog.Format("probe.unreadableOutput", reason);
    }
}
