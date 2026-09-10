namespace QuickSIP.Core.Tones;

public static class TonePcmGenerator
{
    public const int DefaultSampleRate = 8000;

    public static short[] Generate(ToneSpec spec, int durationMs, int sampleRate = DefaultSampleRate, double gain = 0.22)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        int samples = (int)(sampleRate * (durationMs / 1000.0));
        var buffer = new short[samples];
        if (spec.Kind == ToneKind.Silence || (spec.Frequency1 <= 0 && spec.Frequency2 <= 0))
        {
            return buffer;
        }

        double amp = Math.Clamp(gain, 0.01, 0.5);
        double twoPi = 2.0 * Math.PI;
        int onSamples = spec.OnMs > 0 ? (int)(sampleRate * (spec.OnMs / 1000.0)) : samples;
        int offSamples = spec.OffMs > 0 ? (int)(sampleRate * (spec.OffMs / 1000.0)) : 0;
        int cycle = Math.Max(1, onSamples + offSamples);

        for (int i = 0; i < samples; i++)
        {
            bool audible = offSamples <= 0 || (i % cycle) < onSamples;
            if (!audible)
            {
                continue;
            }

            double t = i / (double)sampleRate;
            double sample = 0;
            int tones = 0;
            if (spec.Frequency1 > 0)
            {
                sample += Math.Sin(twoPi * spec.Frequency1 * t);
                tones++;
            }

            if (spec.Frequency2 > 0)
            {
                sample += Math.Sin(twoPi * spec.Frequency2 * t);
                tones++;
            }

            if (tones > 1)
            {
                sample /= tones;
            }

            buffer[i] = (short)Math.Clamp(sample * amp * short.MaxValue, short.MinValue, short.MaxValue);
        }

        return buffer;
    }
}
