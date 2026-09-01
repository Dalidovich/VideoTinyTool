using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class MediaProbeTests
{
    private const string VideoAndAudio = """
    {
      "streams": [
        {
          "codec_type": "video",
          "codec_name": "h264",
          "width": 1920,
          "height": 1080,
          "r_frame_rate": "30/1",
          "duration": "12.480000"
        },
        {
          "codec_type": "audio",
          "codec_name": "aac",
          "sample_rate": "48000"
        }
      ],
      "format": { "duration": "12.520000" }
    }
    """;

    private const string VideoOnly = """
    {
      "streams": [
        {
          "codec_type": "video",
          "codec_name": "hevc",
          "width": 3840,
          "height": 2160,
          "r_frame_rate": "30000/1001",
          "duration": "8.000000"
        }
      ],
      "format": { "duration": "8.000000" }
    }
    """;

    private const string AudioOnlyFromFormat = """
    {
      "streams": [ { "codec_type": "audio", "codec_name": "mp3" } ],
      "format": { "duration": "60.0" }
    }
    """;

    private const string AudioOnlyWithStreamDuration = """
    {
      "streams": [
        {
          "codec_type": "audio",
          "codec_name": "flac",
          "sample_rate": "44100",
          "duration": "184.400000"
        }
      ],
      "format": { "duration": "190.0" }
    }
    """;

    private const string AudioWithCoverArt = """
    {
      "streams": [
        {
          "codec_type": "audio",
          "codec_name": "mp3",
          "duration": "210.5"
        },
        {
          "codec_type": "video",
          "codec_name": "mjpeg",
          "width": 600,
          "height": 600,
          "r_frame_rate": "90000/1",
          "disposition": { "attached_pic": 1 }
        }
      ],
      "format": { "duration": "210.5" }
    }
    """;

    private const string NoStreamsAtAll = """
    {
      "streams": [],
      "format": { "duration": "3.0" }
    }
    """;

    private const string DurationOnlyInFormat = """
    {
      "streams": [
        {
          "codec_type": "video",
          "codec_name": "vp9",
          "width": 1280,
          "height": 720,
          "r_frame_rate": "25/1"
        }
      ],
      "format": { "duration": "42.5" }
    }
    """;

    private const string CoverArtThenVideo = """
    {
      "streams": [
        {
          "codec_type": "video",
          "codec_name": "mjpeg",
          "width": 600,
          "height": 600,
          "r_frame_rate": "90000/1",
          "disposition": { "attached_pic": 1 }
        },
        {
          "codec_type": "video",
          "codec_name": "h264",
          "width": 1280,
          "height": 720,
          "r_frame_rate": "24/1",
          "duration": "5.0",
          "disposition": { "attached_pic": 0 }
        }
      ],
      "format": { "duration": "5.0" }
    }
    """;

    [Fact]
    public void ReadsAnOrdinaryFile()
    {
        var source = MediaProbe.ParseProbeJson(VideoAndAudio, @"C:\media\intro.mp4");

        Assert.Equal(@"C:\media\intro.mp4", source.Path);
        Assert.Equal("intro.mp4", source.FileName);
        Assert.Equal(1920, source.Width);
        Assert.Equal(1080, source.Height);
        Assert.Equal(30, source.FrameRate, 3);
        Assert.Equal("h264", source.VideoCodec);
        Assert.Equal("aac", source.AudioCodec);
        Assert.True(source.HasAudio);
        Assert.Equal(12.48, source.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void ReadsAFileWithoutAudio()
    {
        var source = MediaProbe.ParseProbeJson(VideoOnly, @"C:\media\silent.mp4");

        Assert.False(source.HasAudio);
        Assert.Null(source.AudioCodec);
        Assert.Equal(3840, source.Width);
    }

    [Fact]
    public void ReadsNtscFrameRates()
    {
        var source = MediaProbe.ParseProbeJson(VideoOnly, @"C:\media\silent.mp4");

        Assert.Equal(29.97, source.FrameRate, 2);
    }

    [Fact]
    public void FallsBackToTheFormatDurationWhenTheStreamHasNone()
    {
        var source = MediaProbe.ParseProbeJson(DurationOnlyInFormat, @"C:\media\clip.webm");

        Assert.Equal(TimeSpan.FromSeconds(42.5), source.Duration);
    }

    [Fact]
    public void SkipsCoverArtAndPicksTheRealVideoStream()
    {
        var source = MediaProbe.ParseProbeJson(CoverArtThenVideo, @"C:\media\album.mp4");

        Assert.Equal("h264", source.VideoCodec);
        Assert.Equal(1280, source.Width);
        Assert.Equal(24, source.FrameRate, 3);
    }

    [Fact]
    public void ReadsAnAudioOnlyFileWithTheFormatDuration()
    {
        var source = MediaProbe.ParseProbeJson(AudioOnlyFromFormat, @"C:\media\song.mp3");

        Assert.False(source.HasVideo);
        Assert.Null(source.VideoCodec);
        Assert.True(source.HasAudio);
        Assert.Equal("mp3", source.AudioCodec);
        Assert.Equal(0, source.Width);
        Assert.Equal(0, source.Height);
        Assert.Equal(25, source.FrameRate, 3);
        Assert.Equal(16.0 / 9.0, source.AspectRatio, 5);
        Assert.Equal(TimeSpan.FromSeconds(60), source.Duration);
    }

    [Fact]
    public void PrefersTheAudioStreamDurationOverTheFormatDuration()
    {
        var source = MediaProbe.ParseProbeJson(AudioOnlyWithStreamDuration, @"C:\media\take.flac");

        Assert.False(source.HasVideo);
        Assert.Equal("flac", source.AudioCodec);
        Assert.Equal(184.4, source.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void ReadsAnMp3WithACoverPictureAsAudioOnly()
    {
        var source = MediaProbe.ParseProbeJson(AudioWithCoverArt, @"C:\media\album.mp3");

        Assert.False(source.HasVideo);
        Assert.Null(source.VideoCodec);
        Assert.Equal(0, source.Width);
        Assert.Equal(0, source.Height);
        Assert.Equal(210.5, source.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void RejectsAFileWithNoPlayableStream()
    {
        var error = Assert.Throws<ProbeFailedException>(() =>
            MediaProbe.ParseProbeJson(NoStreamsAtAll, @"C:\media\empty.mp4"));

        Assert.Contains("no video or audio stream", error.Message);
    }

    [Fact]
    public void RejectsUnreadableOutput()
    {
        Assert.Throws<ProbeFailedException>(() => MediaProbe.ParseProbeJson("not json at all", @"C:\media\x.mp4"));
    }

    [Theory]
    [InlineData("30/1", 30)]
    [InlineData("30000/1001", 29.97002997)]
    [InlineData("25", 25)]
    [InlineData("0/0", 25)]
    [InlineData("", 25)]
    [InlineData(null, 25)]
    public void ParsesFrameRateFractions(string? value, double expected)
    {
        Assert.Equal(expected, MediaProbe.ParseFrameRate(value), 5);
    }
}
