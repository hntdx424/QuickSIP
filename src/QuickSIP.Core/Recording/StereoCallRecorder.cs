using QuickSIP.Core.Recording;

namespace QuickSIP.Core.Recording;

/// <summary>
/// Aligns local (left) and remote (right) PCM streams into an 8 kHz stereo WAV.
/// </summary>
public sealed class StereoCallRecorder : IDisposable
{
    private readonly StereoPcm8kWavWriter _writer;
    private readonly Queue<short> _left = new();
    private readonly Queue<short> _right = new();
    private readonly object _gate = new();
    private bool _disposed;

    public StereoCallRecorder(string path)
    {
        FilePath = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        _writer = new StereoPcm8kWavWriter(path);
    }

    public string FilePath { get; }
    public bool IsRecording => !_disposed;

    public void AddLocal(ReadOnlySpan<short> pcm, int sampleRate)
    {
        Add(_left, pcm, sampleRate);
        FlushAligned();
    }

    public void AddRemote(ReadOnlySpan<short> pcm, int sampleRate)
    {
        Add(_right, pcm, sampleRate);
        FlushAligned();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            while (_left.Count > 0 || _right.Count > 0)
            {
                short l = _left.Count > 0 ? _left.Dequeue() : (short)0;
                short r = _right.Count > 0 ? _right.Dequeue() : (short)0;
                _writer.WritePair(l, r);
            }

            _writer.Dispose();
            _disposed = true;
        }
    }

    private void Add(Queue<short> queue, ReadOnlySpan<short> pcm, int sampleRate)
    {
        if (pcm.IsEmpty)
        {
            return;
        }

        var at8k = StereoPcm8kWavWriter.DownsampleTo8k(pcm, sampleRate);
        lock (_gate)
        {
            ThrowIfDisposed();
            foreach (var s in at8k)
            {
                queue.Enqueue(s);
            }
        }
    }

    private void FlushAligned()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            int n = Math.Min(_left.Count, _right.Count);
            for (int i = 0; i < n; i++)
            {
                _writer.WritePair(_left.Dequeue(), _right.Dequeue());
            }
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
