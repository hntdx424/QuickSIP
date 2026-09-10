namespace QuickSIP.Core.Tones;

/// <summary>
/// Dual-frequency telephony tone with an optional on/off cadence.
/// Frequencies are Hz. A zero <see cref="OffMs"/> means continuous.
/// </summary>
public sealed record ToneSpec(
    ToneKind Kind,
    double Frequency1,
    double Frequency2,
    int OnMs,
    int OffMs,
    int? PlayMs = null)
{
    public bool IsContinuous => OffMs <= 0 && PlayMs is null;
    public bool IsDualTone => Frequency1 > 0 && Frequency2 > 0 && Math.Abs(Frequency1 - Frequency2) > 0.01;
}
