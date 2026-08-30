namespace VideoTinyTool.Media;

public interface IPcmSource
{
    int Read(Span<byte> destination);
}
