namespace QuickSIP.Core.Tones;

/// <summary>
/// North-American progress tones used by QuickSIP.
/// </summary>
public static class ToneCatalog
{
    public static readonly ToneSpec Dial = new(ToneKind.Dial, 350, 440, OnMs: 0, OffMs: 0);
    public static readonly ToneSpec Ringback = new(ToneKind.Ringback, 440, 480, OnMs: 2000, OffMs: 4000);
    public static readonly ToneSpec Ringtone = new(ToneKind.Ringtone, 440, 480, OnMs: 1000, OffMs: 3000);
    public static readonly ToneSpec Busy = new(ToneKind.Busy, 480, 620, OnMs: 500, OffMs: 500);
    public static readonly ToneSpec Congestion = new(ToneKind.Congestion, 480, 620, OnMs: 250, OffMs: 250);
    public static readonly ToneSpec Answer = new(ToneKind.Answer, 425, 0, OnMs: 180, OffMs: 0, PlayMs: 180);
    public static readonly ToneSpec Hangup = new(ToneKind.Hangup, 480, 440, OnMs: 400, OffMs: 0, PlayMs: 400);
    public static readonly ToneSpec Error = new(ToneKind.Error, 480, 620, OnMs: 250, OffMs: 250, PlayMs: 1500);

    public static ToneSpec Get(ToneKind kind) => kind switch
    {
        ToneKind.Dial => Dial,
        ToneKind.Ringback => Ringback,
        ToneKind.Ringtone => Ringtone,
        ToneKind.Busy => Busy,
        ToneKind.Congestion => Congestion,
        ToneKind.Answer => Answer,
        ToneKind.Hangup => Hangup,
        ToneKind.Error => Error,
        _ => new ToneSpec(ToneKind.Silence, 0, 0, 0, 0)
    };
}
