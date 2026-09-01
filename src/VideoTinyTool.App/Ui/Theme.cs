using SFML.Graphics;

namespace VideoTinyTool.Ui;

public static class Theme
{
    public static readonly Color Background = Rgb(0x12171B);
    public static readonly Color Chrome = Rgb(0x1A2126);
    public static readonly Color Panel = Rgb(0x1E262C);
    public static readonly Color Sunk = Rgb(0x0B0E11);
    public static readonly Color FrameVoid = Rgb(0x07090B);
    public static readonly Color Line = Rgb(0x2C363E);
    public static readonly Color LineStrong = Rgb(0x374551);

    public static readonly Color Text = Rgb(0xD3DCE2);
    public static readonly Color TextDim = Rgb(0x7C8B96);
    public static readonly Color TextFaint = Rgb(0x5C6971);

    public static readonly Color Accent = Rgb(0xF2A03D);
    public static readonly Color AccentBorder = Rgb(0xC77E22);
    public static readonly Color AccentInk = Rgb(0x1B1206);
    public static readonly Color AccentSoft = new(0xF2, 0xA0, 0x3D, 0x38);

    public static readonly Color ButtonFace = Rgb(0x252E35);
    public static readonly Color ButtonHover = Rgb(0x2F3A43);
    public static readonly Color ButtonActive = Rgb(0x384552);
    public static readonly Color AccentHover = Rgb(0xF7B461);

    public static readonly Color RowSelected = Rgb(0x25313A);
    public static readonly Color RowHover = Rgb(0x222B32);

    public static readonly Color RulerFace = Rgb(0x171E23);
    public static readonly Color LaneFace = Rgb(0x161D22);
    public static readonly Color ClipFace = Rgb(0x31505F);
    public static readonly Color ClipFaceAlt = Rgb(0x3E6577);
    public static readonly Color ClipSelected = Rgb(0x4E7C90);
    public static readonly Color ClipBorder = Rgb(0x24404E);
    public static readonly Color ClipOverlayFace = Rgb(0x4B3A63);
    public static readonly Color ClipOverlayBorder = Rgb(0x33264A);
    public static readonly Color ClipAudioFace = Rgb(0x2F5A4A);
    public static readonly Color ClipAudioBorder = Rgb(0x1F4034);
    public static readonly Color ClipMissing = Rgb(0x5A2B2B);
    public static readonly Color ClipMissingBorder = Rgb(0x8A4040);

    public static readonly Color TrackGroove = Rgb(0x2A343C);
    public static readonly Color Shade = new(0, 0, 0, 0x9E);
    public static readonly Color DialogFace = Rgb(0x1E262C);

    public const int ToolbarHeight = 38;
    public const int PanelHeaderHeight = 26;
    public const int TimelineFooterHeight = 26;
    public const int RulerHeight = 22;
    public const int TrackHeaderWidth = 46;
    public const int TransportHeight = 34;
    public const int OverlayBarHeight = 30;

    public const int SourceRowHeight = 47;
    public const int SourceThumbWidth = 58;
    public const int SourceThumbHeight = 33;

    public const uint FontSizeBody = 13;
    public const uint FontSizeSmall = 12;
    public const uint FontSizeLabel = 11;
    public const uint FontSizeBrand = 16;

    public const int Padding = 10;

    public static Color Dim(Color color, float factor) => new(
        (byte)(color.R * factor),
        (byte)(color.G * factor),
        (byte)(color.B * factor),
        color.A);

    public static Color WithAlpha(Color color, byte alpha) => new(color.R, color.G, color.B, alpha);

    private static Color Rgb(int value) => new(
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)(value & 0xFF));
}
