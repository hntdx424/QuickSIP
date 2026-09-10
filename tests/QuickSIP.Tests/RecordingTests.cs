using System.Buffers.Binary;
using System.Text;
using QuickSIP.Core.Recording;
using Xunit;

namespace QuickSIP.Tests;

public sealed class RecordingTests
{
    [Fact]
    public void WavHeader_IsStereo8kHzPcm()
    {
        using var ms = new MemoryStream();
        using (var writer = new StereoPcm8kWavWriter(ms, leaveOpen: true))
        {
            writer.WritePair(1234, -1234);
            writer.WritePair(0, 32767);
        }

        var bytes = ms.ToArray();
        Assert.True(bytes.Length >= 52);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(22)));
        Assert.Equal(8000, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(34)));
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)));
        Assert.Equal(1234, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44)));
        Assert.Equal(-1234, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(46)));
    }

    [Fact]
    public void Downsample_16kTo8k_TakesEveryOtherSample()
    {
        short[] src = [1, 2, 3, 4, 5, 6];
        var down = StereoPcm8kWavWriter.DownsampleTo8k(src, 16000);
        Assert.Equal(new short[] { 1, 3, 5 }, down);
    }

    [Fact]
    public void StereoCallRecorder_AlignsChannels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qs-{Guid.NewGuid():N}.wav");
        try
        {
            using (var rec = new StereoCallRecorder(path))
            {
                rec.AddLocal(new short[] { 10, 20, 30, 40 }, 16000);
                rec.AddRemote(new short[] { 7, 8 }, 8000);
            }

            var bytes = File.ReadAllBytes(path);
            Assert.Equal(10, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44)));
            Assert.Equal(7, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(46)));
            Assert.Equal(30, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(48)));
            Assert.Equal(8, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(50)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
