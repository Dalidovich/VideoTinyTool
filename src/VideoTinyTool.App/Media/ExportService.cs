using NReco.VideoConverter;
using VideoTinyTool.Application;

namespace VideoTinyTool.Media;

public enum ExportOutcome
{
    Completed,
    Cancelled,
    Failed
}

public sealed record ExportResult(ExportOutcome Outcome, string OutputPath, string? ErrorMessage);

public sealed class ExportService : IDisposable
{
    private readonly object _gate = new();
    private FFMpegConverter? _converter;
    private Thread? _worker;
    private volatile bool _cancelRequested;

    public bool IsRunning { get; private set; }

    public double Progress { get; private set; }

    public string CurrentOutputPath { get; private set; } = string.Empty;

    public event Action<ExportResult>? Finished;

    public void Start(IReadOnlyList<ExportItem> items, ExportSettings export, string outputPath, TimeSpan totalDuration) =>
        Start(items, [], [], export, outputPath, totalDuration);

    public void Start(
        IReadOnlyList<ExportItem> items,
        IReadOnlyList<OverlayItem> overlays,
        ExportSettings export,
        string outputPath,
        TimeSpan totalDuration) =>
        Start(items, overlays, [], export, outputPath, totalDuration);

    public void Start(
        IReadOnlyList<ExportItem> items,
        IReadOnlyList<OverlayItem> overlays,
        IReadOnlyList<AudioItem> audio,
        ExportSettings export,
        string outputPath,
        TimeSpan totalDuration)
    {
        lock (_gate)
        {
            if (IsRunning)
            {
                return;
            }

            IsRunning = true;
            Progress = 0;
            _cancelRequested = false;
            CurrentOutputPath = outputPath;
        }

        var arguments = FFmpegArgumentBuilder.Build(items, overlays, audio, export, outputPath);

        _worker = new Thread(() => Run(arguments, outputPath, totalDuration))
        {
            IsBackground = true,
            Name = "VideoTinyTool.Export"
        };
        _worker.Start();
    }

    public void Cancel()
    {
        FFMpegConverter? converter;
        lock (_gate)
        {
            if (!IsRunning || _cancelRequested)
            {
                return;
            }

            _cancelRequested = true;
            converter = _converter;
        }

        if (converter is null)
        {
            return;
        }

        var killer = new Thread(() =>
        {
            try
            {
                converter.Stop();
            }
            catch
            {
                // Falls through to Abort below.
            }

            if (!(_worker?.Join(TimeSpan.FromSeconds(3)) ?? true))
            {
                try
                {
                    converter.Abort();
                }
                catch
                {
                    // Nothing else can be done here.
                }
            }
        })
        {
            IsBackground = true,
            Name = "VideoTinyTool.ExportCancel"
        };
        killer.Start();
    }

    public void WaitForExit(TimeSpan timeout) => _worker?.Join(timeout);

    private void Run(string arguments, string outputPath, TimeSpan totalDuration)
    {
        var errorLines = new List<string>();
        ExportResult result;

        var converter = FFmpegRuntime.CreateConverter();
        lock (_gate)
        {
            _converter = converter;
        }

        void OnLog(object? sender, FFMpegLogEventArgs e)
        {
            var line = e.Data;
            if (line is null)
            {
                return;
            }

            if (FFmpegLogParser.TryParseTime(line, out var position) && totalDuration > TimeSpan.Zero)
            {
                Progress = Math.Clamp(position.TotalSeconds / totalDuration.TotalSeconds, 0, 1);
            }
            else if (line.Trim().Length > 0)
            {
                errorLines.Add(line.Trim());
                if (errorLines.Count > 40)
                {
                    errorLines.RemoveAt(0);
                }
            }
        }

        converter.LogReceived += OnLog;

        try
        {
            converter.Invoke(arguments);
            result = _cancelRequested
                ? new ExportResult(ExportOutcome.Cancelled, outputPath, null)
                : new ExportResult(ExportOutcome.Completed, outputPath, null);

            if (!_cancelRequested)
            {
                Progress = 1;
            }
        }
        catch (Exception ex)
        {
            result = _cancelRequested
                ? new ExportResult(ExportOutcome.Cancelled, outputPath, null)
                : new ExportResult(ExportOutcome.Failed, outputPath, BuildErrorMessage(ex, errorLines));
        }
        finally
        {
            converter.LogReceived -= OnLog;
            lock (_gate)
            {
                _converter = null;
                IsRunning = false;
            }
        }

        if (result.Outcome != ExportOutcome.Completed)
        {
            TryDeletePartialFile(outputPath);
        }

        Finished?.Invoke(result);
    }

    private static string BuildErrorMessage(Exception exception, IReadOnlyList<string> logLines)
    {
        var tail = logLines.Count == 0
            ? string.Empty
            : "\n\n" + string.Join("\n", logLines.TakeLast(12));

        return exception.Message.Trim() + tail;
    }

    private static void TryDeletePartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A locked partial file is left in place on purpose.
        }
    }

    public void Dispose()
    {
        Cancel();
        _worker?.Join(TimeSpan.FromSeconds(5));
    }
}
