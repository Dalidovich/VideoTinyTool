using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class PcmRingBufferTests
{
    [Fact]
    public void ReadsBackWhatWasWritten()
    {
        var ring = new PcmRingBuffer(64);
        var data = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

        ring.Write(data, CancellationToken.None);

        var destination = new byte[32];
        Assert.Equal(32, ring.Read(destination));
        Assert.Equal(data, destination);
    }

    [Fact]
    public void ReadReturnsOnlyWhatIsAvailable()
    {
        var ring = new PcmRingBuffer(64);
        ring.Write(new byte[10], CancellationToken.None);

        Assert.Equal(10, ring.Read(new byte[40]));
        Assert.Equal(0, ring.Available);
    }

    [Fact]
    public void WrapsAroundTheEndOfTheBuffer()
    {
        var ring = new PcmRingBuffer(16);
        ring.Write(new byte[12], CancellationToken.None);
        ring.Read(new byte[12]);

        var data = Enumerable.Range(1, 10).Select(i => (byte)i).ToArray();
        ring.Write(data, CancellationToken.None);

        var destination = new byte[10];
        Assert.Equal(10, ring.Read(destination));
        Assert.Equal(data, destination);
    }

    [Fact]
    public void ClearDropsPendingData()
    {
        var ring = new PcmRingBuffer(32);
        ring.Write(new byte[16], CancellationToken.None);

        ring.Clear();

        Assert.Equal(0, ring.Available);
        Assert.Equal(0, ring.Read(new byte[16]));
    }

    [Fact]
    public void WriteToAClosedBufferIsDroppedInsteadOfBlocking()
    {
        var ring = new PcmRingBuffer(8);
        ring.Close();

        ring.Write(new byte[64], CancellationToken.None);

        Assert.Equal(0, ring.Available);
    }

    [Fact]
    public async Task WriteBlocksUntilAReaderMakesRoom()
    {
        var ring = new PcmRingBuffer(16);
        ring.Write(new byte[16], CancellationToken.None);

        var writer = Task.Run(() => ring.Write(new byte[8], CancellationToken.None));
        var early = await Task.WhenAny(writer, Task.Delay(TimeSpan.FromMilliseconds(120)));
        Assert.NotSame(writer, early);

        ring.Read(new byte[16]);

        var finished = await Task.WhenAny(writer, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(writer, finished);
        Assert.Equal(8, ring.Available);
    }
}
