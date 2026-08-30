using System.Diagnostics;
using System.Runtime.Versioning;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using VideoTinyTool.Commands;
using VideoTinyTool.Domain;
using VideoTinyTool.Localization;
using VideoTinyTool.Media;
using VideoTinyTool.Platform;
using VideoTinyTool.Ui;
using VideoTinyTool.Ui.Panels;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Application;

[SupportedOSPlatform("windows")]
public sealed class EditorApplication : IEditorHost, IDisposable
{
    private static readonly double[] ShuttleRates = [1, 2, 4];
    private static readonly TimeSpan ReverseShuttleStep = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan MissingScanInterval = TimeSpan.FromSeconds(2);

    private readonly AppSettings _settings;
    private readonly RenderWindow _window;
    private readonly FontCatalog _fonts;
    private readonly Renderer _renderer;
    private readonly InputRouter _input = new();

    private readonly Timeline _timeline = new();
    private readonly CommandHistory _history;
    private readonly List<MediaSource> _sources = new();
    private readonly Dictionary<Guid, MediaSource> _sourceIndex = new();
    private readonly HashSet<Guid> _missingSources = new();

    private readonly ThumbnailService _thumbnails = new();
    private readonly StillFrameService _stillFrames = new();
    private readonly PreviewPlayer _player;
    private readonly ExportService _export = new();

    private readonly ToolbarPanel _toolbar;
    private readonly SourcesPanel _sourcesPanel;
    private readonly PreviewPanel _previewPanel;
    private readonly TimelinePanel _timelinePanel;

    private readonly Queue<ModalDialog> _dialogQueue = new();
    private ModalDialog? _dialog;
    private ProgressDialog? _progressDialog;
    private ExportResult? _exportResult;

    private EditorLayout _layout;
    private Vector2f _pointer;
    private MediaSource? _selectedSource;
    private Clip? _selectedClip;
    private int _shuttleIndex;
    private int _shuttleDirection;
    private readonly Stopwatch _reverseShuttle = new();
    private readonly Stopwatch _missingScan = Stopwatch.StartNew();
    private bool _closeRequested;
    private bool _importInProgress;
    private bool _disposed;

    public EditorApplication(AppSettings settings)
    {
        _settings = settings;
        _history = new CommandHistory(_timeline);

        _window = new RenderWindow(
            new VideoMode(new Vector2u((uint)settings.Window.Width, (uint)settings.Window.Height)),
            I18n.Brand.Full,
            Styles.Default,
            State.Windowed);

        WindowIcon.Apply(_window.NativeHandle);

        _window.SetFramerateLimit(60);
        _window.SetMinimumSize(new Vector2u(
            (uint)LayoutCalculator.MinimumWindowWidth,
            (uint)LayoutCalculator.MinimumWindowHeight));

        _fonts = FontCatalog.Load();
        _renderer = new Renderer(_window, _fonts);

        _player = new PreviewPlayer(
            _timeline,
            FindSource,
            _stillFrames,
            settings.Preview.Width,
            settings.Preview.Height);

        _toolbar = new ToolbarPanel(this);
        _sourcesPanel = new SourcesPanel(this);
        _previewPanel = new PreviewPanel(this);
        _timelinePanel = new TimelinePanel(this);

        _input.Register(_toolbar);
        _input.Register(_sourcesPanel);
        _input.Register(_previewPanel);
        _input.Register(_timelinePanel);

        _window.Closed += (_, _) => RequestClose();
        _window.Resized += (_, e) => OnResized(e.Size);
        _window.KeyPressed += (_, e) => OnKeyPressed(e);
        _window.MouseButtonPressed += (_, e) => OnMouseDown(e);
        _window.MouseButtonReleased += (_, e) => OnMouseUp(e);
        _window.MouseMoved += (_, e) => OnMouseMove(e);
        _window.MouseWheelScrolled += (_, e) => OnScroll(e);
        _window.LostFocus += (_, _) => _input.ReleaseCapture();

        _export.Finished += result => _exportResult = result;

        Relayout(_window.Size);
        ReportStartupProblems();
    }

    public Timeline Timeline => _timeline;

    public IReadOnlyList<MediaSource> Sources => _sources;

    public MediaSource? SelectedSource => _selectedSource;

    public Clip? SelectedClip => _selectedClip;

    public int SelectedTrackIndex => _selectedClip is null ? 0 : Math.Max(0, _timeline.TrackIndexOf(_selectedClip));

    public PreviewPlayer Player => _player;

    public ThumbnailService Thumbnails => _thumbnails;

    public CommandHistory History => _history;

    public AppSettings Settings => _settings;

    public void Run()
    {
        while (_window.IsOpen)
        {
            _window.DispatchEvents();

            _thumbnails.PumpCompleted();
            _player.Update();
            AdvanceReverseShuttle();
            PumpExport();
            PumpDialogs();
            ScanForMissingSources();

            if (_player.IsPlaying)
            {
                _timelinePanel.ScrollToPlayhead();
            }

            Draw();
        }
    }

    private void Draw()
    {
        _renderer.BeginFrame();
        _window.Clear(Theme.Background);

        _toolbar.Layout(_renderer);
        _previewPanel.Layout(_renderer);
        _timelinePanel.Layout(_renderer);

        _toolbar.Draw(_renderer);
        _sourcesPanel.Draw(_renderer);
        _previewPanel.Draw(_renderer);
        _timelinePanel.Draw(_renderer);

        if (_dialog is not null)
        {
            _dialog.Layout(_renderer, _window.Size.X, _window.Size.Y);
            _dialog.UpdateHover(_pointer);
            _dialog.Draw(_renderer, _window.Size.X, _window.Size.Y);
        }

        _window.Display();
    }

    private void Relayout(Vector2u size)
    {
        _layout = LayoutCalculator.Compute(size.X, size.Y);
        _toolbar.Bounds = _layout.Toolbar;
        _sourcesPanel.Bounds = _layout.Sources;
        _previewPanel.Bounds = _layout.Preview;
        _timelinePanel.Bounds = _layout.Timeline;
    }

    private void OnResized(Vector2u size)
    {
        var width = Math.Max(size.X, (uint)LayoutCalculator.MinimumWindowWidth);
        var height = Math.Max(size.Y, (uint)LayoutCalculator.MinimumWindowHeight);

        _renderer.SetWindowSize(width, height);
        Relayout(new Vector2u(width, height));
    }

    private void OnMouseDown(MouseButtonEventArgs e)
    {
        var point = new Vector2f(e.Position.X, e.Position.Y);
        _pointer = point;

        if (_dialog is not null)
        {
            _dialog.OnMouseDown(point);
            return;
        }

        _input.MouseDown(point, e.Button);
    }

    private void OnMouseUp(MouseButtonEventArgs e)
    {
        var point = new Vector2f(e.Position.X, e.Position.Y);
        _pointer = point;

        if (_dialog is not null)
        {
            _dialog.OnMouseUp(point);
            return;
        }

        _input.MouseUp(point, e.Button);
    }

    private void OnMouseMove(MouseMoveEventArgs e)
    {
        var point = new Vector2f(e.Position.X, e.Position.Y);
        _pointer = point;

        if (_dialog is not null)
        {
            _dialog.UpdateHover(point);
            return;
        }

        _input.MouseMove(point);
    }

    private void OnScroll(MouseWheelScrollEventArgs e)
    {
        if (_dialog is not null)
        {
            return;
        }

        _input.Scroll(
            new Vector2f(e.Position.X, e.Position.Y),
            e.Delta,
            Keyboard.IsKeyPressed(Keyboard.Key.LControl) || Keyboard.IsKeyPressed(Keyboard.Key.RControl));
    }

    private void OnKeyPressed(KeyEventArgs e)
    {
        if (_dialog is not null)
        {
            if (e.Code == Keyboard.Key.Escape)
            {
                var cancel = _dialog.Buttons.LastOrDefault();
                if (_dialog.Buttons.Count > 1)
                {
                    cancel?.OnMouseDown(cancel.Bounds.Center);
                    cancel?.OnMouseUp(cancel.Bounds.Center);
                }
                else
                {
                    _dialog.Close();
                }
            }
            else if (e.Code is Keyboard.Key.Enter or Keyboard.Key.Space)
            {
                var accept = _dialog.Buttons.FirstOrDefault();
                accept?.OnMouseDown(accept.Bounds.Center);
                accept?.OnMouseUp(accept.Bounds.Center);
            }

            return;
        }

        Dispatch(Shortcuts.Resolve(e));
    }

    private void Dispatch(EditorCommand command)
    {
        switch (command)
        {
            case EditorCommand.TogglePlay:
                StopShuttle();
                _player.TogglePlay();
                break;

            case EditorCommand.ShuttleForward:
                Shuttle(1);
                break;

            case EditorCommand.ShuttleBackward:
                Shuttle(-1);
                break;

            case EditorCommand.ShuttleStop:
                StopShuttle();
                _player.Pause();
                break;

            case EditorCommand.StepFrameBack:
                StepBy(-FrameStep());
                break;

            case EditorCommand.StepFrameForward:
                StepBy(FrameStep());
                break;

            case EditorCommand.StepSecondBack:
                StepBy(TimeSpan.FromSeconds(-1));
                break;

            case EditorCommand.StepSecondForward:
                StepBy(TimeSpan.FromSeconds(1));
                break;

            case EditorCommand.GoToStart:
                SeekTo(TimeSpan.Zero, false);
                break;

            case EditorCommand.GoToEnd:
                SeekTo(_timeline.TotalDuration, false);
                break;

            case EditorCommand.TrimIn:
                TrimSelectedToPlayhead(true);
                break;

            case EditorCommand.TrimOut:
                TrimSelectedToPlayhead(false);
                break;

            case EditorCommand.Split:
                SplitAtPlayhead();
                break;

            case EditorCommand.Delete:
                RemoveSelectedClip();
                break;

            case EditorCommand.RippleDelete:
                RippleDeleteSelectedClip();
                break;

            case EditorCommand.Undo:
                Undo();
                break;

            case EditorCommand.Redo:
                Redo();
                break;

            case EditorCommand.Import:
                ImportFiles();
                break;

            case EditorCommand.Export:
                ExportTimeline();
                break;

            case EditorCommand.ZoomIn:
                _timelinePanel.ZoomBy(1.25f);
                break;

            case EditorCommand.ZoomOut:
                _timelinePanel.ZoomBy(1f / 1.25f);
                break;

            case EditorCommand.AddTrack:
                AddTrack();
                break;

            case EditorCommand.RemoveTrack:
                RemoveTrack(SelectedTrackIndex);
                break;

            case EditorCommand.Help:
                ShowShortcuts();
                break;
        }
    }

    private void Shuttle(int direction)
    {
        if (_timeline.Clips.Count == 0)
        {
            return;
        }

        if (_shuttleDirection == direction)
        {
            _shuttleIndex = Math.Min(_shuttleIndex + 1, ShuttleRates.Length - 1);
        }
        else
        {
            _shuttleDirection = direction;
            _shuttleIndex = 0;
        }

        if (direction < 0)
        {
            _player.Pause();
            _reverseShuttle.Restart();
            return;
        }

        _reverseShuttle.Reset();
        _player.PlayAtRate(ShuttleRates[_shuttleIndex]);
    }

    private void StopShuttle()
    {
        if (_shuttleDirection < 0)
        {
            _player.EndScrub(false);
        }

        _shuttleIndex = 0;
        _shuttleDirection = 0;
        _reverseShuttle.Reset();
    }

    private void AdvanceReverseShuttle()
    {
        if (_shuttleDirection >= 0 || !_reverseShuttle.IsRunning)
        {
            return;
        }

        var elapsed = _reverseShuttle.Elapsed;
        if (elapsed < ReverseShuttleStep)
        {
            return;
        }

        _reverseShuttle.Restart();

        var position = _player.Position - TimeSpan.FromSeconds(elapsed.TotalSeconds * ShuttleRates[_shuttleIndex]);
        if (position <= TimeSpan.Zero)
        {
            StopShuttle();
            SeekTo(TimeSpan.Zero, false);
            return;
        }

        _player.Seek(position, true);
        _timelinePanel.ScrollToPlayhead();
    }

    private TimeSpan FrameStep()
    {
        var location = _timeline.Resolve(_player.Position);
        if (location is null)
        {
            return TimeSpan.FromSeconds(1.0 / 25);
        }

        var source = FindSource(location.Value.Clip.SourceId);
        return source?.FrameDuration ?? TimeSpan.FromSeconds(1.0 / 25);
    }

    private void StepBy(TimeSpan delta) => SeekTo(_player.Position + delta, false);

    public MediaSource? FindSource(Guid id) => _sourceIndex.GetValueOrDefault(id);

    public bool SourceMissing(Guid sourceId) => _missingSources.Contains(sourceId);

    public void SelectSource(MediaSource? source) => _selectedSource = source;

    public void SelectClip(Clip? clip) => _selectedClip = clip;

    public void AppendSourceToTimeline(MediaSource source)
    {
        var clip = Clip.Create(source.Id, TimeSpan.Zero, source.Duration);
        Execute(new AddClipCommand(clip, _timeline.Clips.Count));
        _selectedClip = clip;
    }

    public void RemoveSource(MediaSource source)
    {
        if (_export.IsRunning)
        {
            return;
        }

        var index = _sources.IndexOf(source);
        if (index < 0)
        {
            return;
        }

        var usage = _timeline.Clips.Count(clip => clip.SourceId == source.Id);
        if (usage == 0)
        {
            Execute(BuildRemoveSourceCommand(source, index));
            return;
        }

        var dialog = new ModalDialog(
            I18n.Dialogs.RemoveSourceTitle,
            I18n.Dialogs.RemoveSourceMessage(source.FileName, usage));

        dialog.AddButton(I18n.Dialogs.RemoveSourceConfirm, ButtonStyle.Accent, () =>
        {
            dialog.Close();
            if (_sources.Contains(source))
            {
                Execute(BuildRemoveSourceCommand(source, _sources.IndexOf(source)));
            }
        });

        dialog.AddButton(I18n.Dialogs.Cancel, ButtonStyle.Normal, dialog.Close);
        ShowDialog(dialog);
    }

    private RemoveSourceCommand BuildRemoveSourceCommand(MediaSource source, int index) =>
        new(source, index, DetachSource, RestoreSource);

    private void DetachSource(MediaSource source)
    {
        var index = _sources.IndexOf(source);
        if (index >= 0)
        {
            _sources.RemoveAt(index);
        }

        _sourceIndex.Remove(source.Id);
        _missingSources.Remove(source.Id);
        _thumbnails.Forget(source.Id);

        if (_selectedSource is not null && _selectedSource.Id == source.Id)
        {
            _selectedSource = _sources.Count == 0
                ? null
                : _sources[Math.Clamp(index, 0, _sources.Count - 1)];
        }
    }

    private void RestoreSource(int index, MediaSource source)
    {
        _sources.Insert(Math.Clamp(index, 0, _sources.Count), source);
        _sourceIndex[source.Id] = source;
        _selectedSource ??= source;
    }

    public void AddTrack()
    {
        if (_timeline.Tracks.Count >= Domain.Timeline.MaxTracks)
        {
            return;
        }

        Execute(new AddTrackCommand());
    }

    public void RemoveTrack(int index)
    {
        if (index <= 0 || index >= _timeline.Tracks.Count)
        {
            return;
        }

        Execute(new RemoveTrackCommand(index));
    }

    public void MoveClipToTrack(Clip clip, int trackIndex, TimeSpan start) =>
        Execute(new MoveClipToTrackCommand(clip, trackIndex, start));

    public void SetOverlayTransform(Clip clip, OverlayTransform transform) =>
        Execute(new SetOverlayTransformCommand(clip, transform));

    public void Execute(IEditCommand command)
    {
        _history.Execute(command);
        AfterTimelineChanged();
    }

    public void SeekTo(TimeSpan position, bool scrubbing)
    {
        StopShuttle();
        _player.Seek(position, scrubbing);
        if (!scrubbing)
        {
            _timelinePanel.ScrollToPlayhead();
        }
    }

    public void TogglePlayback()
    {
        if (_timeline.Clips.Count == 0)
        {
            return;
        }

        Dispatch(EditorCommand.TogglePlay);
    }

    public void EndScrub(bool resumePlayback)
    {
        _player.EndScrub(resumePlayback);
        _timelinePanel.ScrollToPlayhead();
    }

    public void ImportFiles()
    {
        if (_importInProgress || _export.IsRunning)
        {
            return;
        }

        if (!FFmpegRuntime.Available)
        {
            ShowMessage(I18n.Dialogs.FFmpegMissingTitle, FFmpegRuntime.MissingBinariesMessage);
            return;
        }

        _importInProgress = true;
        try
        {
            var paths = NativeFileDialog.OpenVideoFiles(_window.NativeHandle);
            var failures = new List<string>();

            foreach (var path in paths)
            {
                if (_sources.Any(existing => string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                try
                {
                    var source = MediaProbe.Probe(path);
                    _sources.Add(source);
                    _sourceIndex[source.Id] = source;
                    _selectedSource ??= source;
                }
                catch (Exception ex)
                {
                    failures.Add(I18n.Dialogs.ImportFailure(Path.GetFileName(path), ex.Message));
                }
            }

            if (failures.Count > 0)
            {
                ShowMessage(
                    I18n.Dialogs.ImportFailedTitle(failures.Count),
                    string.Join("\n", failures));
            }
        }
        finally
        {
            _importInProgress = false;
        }
    }

    public void ExportTimeline()
    {
        if (_timeline.Clips.Count == 0 || _export.IsRunning)
        {
            return;
        }

        if (!FFmpegRuntime.Available)
        {
            ShowMessage(I18n.Dialogs.FFmpegMissingTitle, FFmpegRuntime.MissingBinariesMessage);
            return;
        }

        RefreshMissingSources();
        if (_missingSources.Count > 0)
        {
            ShowMessage(
                I18n.Dialogs.MissingSourcesTitle,
                I18n.Dialogs.MissingSourcesMessage(
                    _missingSources.Select(id => FindSource(id)?.Path ?? id.ToString())));
            return;
        }

        _player.Pause();

        var setup = new ExportSettingsDialog(_settings.Export);

        setup.AddButton(I18n.ExportSetup.Start, ButtonStyle.Accent, () =>
        {
            setup.Close();
            setup.Apply(_settings.Export);
            BeginExport();
        });

        setup.AddButton(I18n.Dialogs.Cancel, ButtonStyle.Normal, setup.Close);
        ShowDialog(setup);
    }

    private void BeginExport()
    {
        var container = _settings.Export.Container;
        var path = NativeFileDialog.SaveFile(_window.NativeHandle, container, I18n.FileDialogs.DefaultExportName(container));
        if (path is null)
        {
            return;
        }

        var items = FFmpegArgumentBuilder.BuildItems(_timeline, _sourceIndex);
        if (items.Count == 0)
        {
            return;
        }

        var overlays = FFmpegArgumentBuilder.BuildOverlayItems(_timeline, _sourceIndex);

        _progressDialog = new ProgressDialog(I18n.Dialogs.ExportingTitle, path);
        _progressDialog.AddButton(I18n.Dialogs.Cancel, ButtonStyle.Normal, () => _export.Cancel());
        ShowDialog(_progressDialog);

        _export.Start(
            items,
            overlays,
            _settings.Export,
            path,
            FFmpegArgumentBuilder.OutputDuration(_timeline.TotalDuration, _settings.Export));
    }

    public void SplitAtPlayhead()
    {
        var clip = _selectedClip;
        if (clip is null)
        {
            return;
        }

        var offsetInClip = _player.Position - _timeline.StartOf(clip);
        var minimum = EditRules.MinimumDuration(FindSource(clip.SourceId));

        if (!EditRules.CanSplit(clip, offsetInClip, minimum))
        {
            return;
        }

        var command = new SplitClipCommand(clip, clip.In + offsetInClip);
        Execute(command);
        _selectedClip = command.Right;
    }

    public void RemoveSelectedClip() => RemoveSelectedClip(ripple: false);

    public void RippleDeleteSelectedClip() => RemoveSelectedClip(ripple: true);

    private void RemoveSelectedClip(bool ripple)
    {
        var clip = _selectedClip;
        if (clip is null)
        {
            return;
        }

        var trackIndex = Math.Max(0, _timeline.TrackIndexOf(clip));
        var index = _timeline.IndexOf(trackIndex, clip);
        Execute(new RemoveClipCommand(clip, ripple));

        var clips = _timeline.ClipsOf(trackIndex);
        _selectedClip = clips.Count == 0 ? null : clips[Math.Clamp(index, 0, clips.Count - 1)];
    }

    public void TrimSelectedToPlayhead(bool trimIn)
    {
        var clip = _selectedClip;
        if (clip is null)
        {
            return;
        }

        var source = FindSource(clip.SourceId);
        var minimum = EditRules.MinimumDuration(source);
        var offsetInClip = _player.Position - _timeline.StartOf(clip);

        if (offsetInClip <= TimeSpan.Zero || offsetInClip >= clip.Duration)
        {
            return;
        }

        if (trimIn)
        {
            if (EditRules.TryTrimIn(clip, clip.In + offsetInClip, minimum, out var newIn))
            {
                Execute(new TrimClipCommand(clip, newIn, clip.Out));
            }

            return;
        }

        var sourceDuration = source?.Duration ?? clip.Out;
        if (EditRules.TryTrimOut(clip, clip.In + offsetInClip, minimum, sourceDuration, out var newOut))
        {
            Execute(new TrimClipCommand(clip, clip.In, newOut));
        }
    }

    public void Undo()
    {
        if (_history.Undo() is null)
        {
            return;
        }

        AfterTimelineChanged();
    }

    public void Redo()
    {
        if (_history.Redo() is null)
        {
            return;
        }

        AfterTimelineChanged();
    }

    private void AfterTimelineChanged()
    {
        if (_selectedClip is not null && _timeline.TrackIndexOf(_selectedClip) < 0)
        {
            _selectedClip = null;
        }

        RefreshMissingSources();
        _player.TimelineChanged();
    }

    private void ScanForMissingSources()
    {
        if (_missingScan.Elapsed < MissingScanInterval)
        {
            return;
        }

        _missingScan.Restart();
        RefreshMissingSources();
    }

    private void RefreshMissingSources()
    {
        _missingSources.Clear();
        foreach (var source in _sources)
        {
            if (!File.Exists(source.Path))
            {
                _missingSources.Add(source.Id);
            }
        }
    }

    private void PumpExport()
    {
        if (_progressDialog is not null && _export.IsRunning)
        {
            _progressDialog.Progress = _export.Progress;
        }

        var result = _exportResult;
        if (result is null)
        {
            return;
        }

        _exportResult = null;

        if (_progressDialog is not null)
        {
            _progressDialog.Close();
            _progressDialog = null;
        }

        switch (result.Outcome)
        {
            case ExportOutcome.Completed:
                ShowMessage(I18n.Dialogs.ExportFinishedTitle, result.OutputPath);
                break;

            case ExportOutcome.Cancelled:
                ShowMessage(I18n.Dialogs.ExportCancelledTitle, I18n.Dialogs.ExportCancelledMessage);
                break;

            case ExportOutcome.Failed:
                ShowMessage(I18n.Dialogs.ExportFailedTitle, result.ErrorMessage ?? I18n.Dialogs.ExportFailedFallback);
                break;
        }

        if (_closeRequested)
        {
            _window.Close();
        }
    }

    private void PumpDialogs()
    {
        if (_dialog is { Closed: true })
        {
            _dialog = null;
        }

        if (_dialog is null && _dialogQueue.Count > 0)
        {
            _dialog = _dialogQueue.Dequeue();
        }
    }

    public void ShowShortcuts() => ShowDialog(new HelpDialog());

    private void ShowMessage(string title, string message)
    {
        var dialog = new ModalDialog(title, message);
        dialog.AddButton(I18n.Dialogs.Ok, ButtonStyle.Accent, dialog.Close);
        ShowDialog(dialog);
    }

    private void ShowDialog(ModalDialog dialog)
    {
        if (_dialog is null)
        {
            _dialog = dialog;
            return;
        }

        _dialogQueue.Enqueue(dialog);
    }

    private void RequestClose()
    {
        if (!_export.IsRunning)
        {
            _window.Close();
            return;
        }

        if (_closeRequested)
        {
            return;
        }

        var dialog = new ModalDialog(
            I18n.Dialogs.ExportRunningTitle,
            I18n.Dialogs.ExportRunningMessage);

        dialog.AddButton(I18n.Dialogs.StopAndClose, ButtonStyle.Accent, () =>
        {
            _closeRequested = true;
            _export.Cancel();
            dialog.Close();
        });

        dialog.AddButton(I18n.Dialogs.KeepExporting, ButtonStyle.Normal, dialog.Close);

        _dialog = dialog;
        _progressDialog = null;
    }

    private void ReportStartupProblems()
    {
        if (_settings.LoadIssue is { } settingsIssue)
        {
            ShowMessage(I18n.Dialogs.SettingsTitle, settingsIssue.Kind switch
            {
                SettingsLoadFailure.Unreadable => I18n.Startup.SettingsUnreadable(settingsIssue.Detail),
                _ => I18n.Startup.SettingsNotCreated(settingsIssue.Detail)
            });
        }

        if (I18n.Warning is { Length: > 0 } localizationWarning)
        {
            ShowMessage(I18n.Dialogs.LocalizationTitle, localizationWarning);
        }

        if (_fonts.Warning is { Length: > 0 } fontWarning)
        {
            ShowMessage(I18n.Dialogs.FontsTitle, fontWarning);
        }

        if (!FFmpegRuntime.Available)
        {
            ShowMessage(I18n.Dialogs.FFmpegMissingTitle, FFmpegRuntime.MissingBinariesMessage);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _export.Dispose();
        _player.Dispose();
        _stillFrames.Dispose();
        _thumbnails.Dispose();
        _renderer.Dispose();
        _fonts.Dispose();
        _window.Dispose();
    }
}
