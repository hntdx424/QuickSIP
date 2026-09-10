using System.Buffers.Binary;
using System.Text;

namespace QuickSIP.Core.Recording;

/// <summary>
/// Writes a 16-bit PCM stereo WAV at 8 kHz (left = local/mic, right = remote).
/// </summary>
public sealed class StereoPcm8kWavWriter : IDisposable
{
    public const int SampleRate = 8000;
    public const short Channels = 2;
    public const short BitsPerSample = 16;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly object _gate = new();
    private int _dataBytes;
    private bool _disposed;

    public StereoPcm8kWavWriter(string path)
        : this(new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read), leaveOpen: false)
    {
    }

    public StereoPcm8kWavWriter(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        WriteHeaderPlaceholder();
    }

    public int FramesWritten { get; private set; }

    public void WritePair(short left, short right)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            Span<byte> frame = stackalloc byte[4];
            BinaryPrimitives.WriteInt16LittleEndian(frame, left);
            BinaryPrimitives.WriteInt16LittleEndian(frame[2..], right);
            _stream.Write(frame);
            _dataBytes += 4;
            FramesWritten++;
        }
    }

    public void WritePairs(ReadOnlySpan<short> left, ReadOnlySpan<short> right)
    {
        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            WritePair(left[i], right[i]);
        }
    }

    public static short[] DownsampleTo8k(ReadOnlySpan<short> samples, int sourceRate)
    {
        if (sourceRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRate));
        }

        if (sourceRate == SampleRate)
        {
            return samples.ToArray();
        }

        if (sourceRate < SampleRate)
        {
            // Upsample by holding samples (rare for VoIP codecs we use).
            double ratio = SampleRate / (double)sourceRate;
            int count = (int)Math.Round(samples.Length * ratio);
            var up = new short[count];
            for (int i = 0; i < count; i++)
            {
                int src = Math.Min(samples.Length - 1, (int)(i / ratio));
                up[i] = samples[src];
            }

            return up;
        }

        int factor = sourceRate / SampleRate;
        if (factor * SampleRate == sourceRate)
        {
            var down = new short[samples.Length / factor];
            for (int i = 0, o = 0; o < down.Length; i += factor, o++)
            {
                down[o] = samples[i];
            }

            return down;
        }

        double step = sourceRate / (double)SampleRate;
        int n = (int)(samples.Length / step);
        var result = new short[n];
        for (int i = 0; i < n; i++)
        {
            int src = Math.Min(samples.Length - 1, (int)(i * step));
            result[i] = samples[src];
        }

        return result;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            PatchHeader();
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }

            _disposed = true;
        }
    }

    private void WriteHeaderPlaceholder()
    {
        // 44-byte canonical PCM header; sizes patched on dispose.
        Span<byte> header = stackalloc byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 36);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(header[8..]);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(header[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], Channels);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], SampleRate);
        int byteRate = SampleRate * Channels * (BitsPerSample / 8);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], byteRate);
        short blockAlign = (short)(Channels * (BitsPerSample / 8));
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], BitsPerSample);
        Encoding.ASCII.GetBytes("data").CopyTo(header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], 0);
        _stream.Write(header);
    }

    private void PatchHeader()
    {
        if (!_stream.CanSeek)
        {
            return;
        }

        int riffSize = 36 + _dataBytes;
        _stream.Position = 4;
        Span<byte> intBuf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(intBuf, riffSize);
        _stream.Write(intBuf);
        _stream.Position = 40;
        BinaryPrimitives.WriteInt32LittleEndian(intBuf, _dataBytes);
        _stream.Write(intBuf);
        _stream.Position = _stream.Length;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
