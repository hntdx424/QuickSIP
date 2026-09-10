using NAudio.Wave;
using QuickSIP.Core.Tones;

namespace QuickSIP.Services;

public interface ITonePlayer : IDisposable
{
    ToneKind Current { get; }
    void Play(ToneKind kind);
    void Stop();
}

/// <summary>
/// Plays dual-frequency telephony tones through the default Windows output device.
/// </summary>
public sealed class TonePlayer : ITonePlayer
{
    private readonly object _gate = new();
    private WaveOut? _waveOut;
    private DualToneSampleProvider? _provider;

    public ToneKind Current { get; private set; } = ToneKind.Silence;

    public void Play(ToneKind kind)
    {
        lock (_gate)
        {
            if (kind == ToneKind.Silence)
            {
                StopCore();
                return;
            }

            if (Current == kind && _waveOut is { PlaybackState: PlaybackState.Playing })
            {
                return;
            }

            StopCore();
            var spec = ToneCatalog.Get(kind);
            _provider = new DualToneSampleProvider(spec);
            try
            {
                _waveOut = new WaveOut();
                _waveOut.Init(_provider);
                _waveOut.Play();
                Current = kind;
                if (spec.PlayMs is int playMs and > 0)
                {
                    var captured = _waveOut;
                    _ = Task.Delay(playMs).ContinueWith(__ =>
                    {
                        lock (_gate)
                        {
                            if (ReferenceEquals(_waveOut, captured))
                            {
                                StopCore();
                            }
                        }
                    });
                }
            }
            catch
            {
                StopCore();
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopCore();
        }
    }

    public void Dispose() => Stop();

    private void StopCore()
    {
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
        }
        catch
        {
            // Ignore audio teardown races on shutdown.
        }

        _waveOut = null;
        _provider = null;
        Current = ToneKind.Silence;
    }

    private sealed class DualToneSampleProvider : ISampleProvider
    {
        private readonly ToneSpec _spec;
        private readonly float _gain = 0.18f;
        private int _sampleIndex;
        private readonly int _onSamples;
        private readonly int _cycleSamples;

        public DualToneSampleProvider(ToneSpec spec)
        {
            _spec = spec;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(8000, 1);
            _onSamples = spec.OnMs > 0 ? (int)(8.0 * spec.OnMs) : int.MaxValue;
            var offSamples = spec.OffMs > 0 ? (int)(8.0 * spec.OffMs) : 0;
            _cycleSamples = offSamples <= 0 ? int.MaxValue : _onSamples + offSamples;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public int Read(Span<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = NextSample();
            }

            return buffer.Length;
        }

        private float NextSample()
        {
            bool on = _cycleSamples == int.MaxValue || (_sampleIndex % _cycleSamples) < _onSamples;
            float sample = 0;
            if (on)
            {
                double t = _sampleIndex / 8000.0;
                int tones = 0;
                if (_spec.Frequency1 > 0)
                {
                    sample += (float)Math.Sin(2 * Math.PI * _spec.Frequency1 * t);
                    tones++;
                }

                if (_spec.Frequency2 > 0)
                {
                    sample += (float)Math.Sin(2 * Math.PI * _spec.Frequency2 * t);
                    tones++;
                }

                if (tones > 1)
                {
                    sample /= tones;
                }

                sample *= _gain;
            }

            _sampleIndex++;
            return sample;
        }
    }
}
