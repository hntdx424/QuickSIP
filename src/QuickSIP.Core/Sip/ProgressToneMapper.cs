using QuickSIP.Core.Tones;

namespace QuickSIP.Core.Sip;

/// <summary>
/// Maps SIP INVITE progress / failure codes to locally generated call-progress tones.
/// </summary>
public static class ProgressToneMapper
{
    public static ToneKind? FromProvisional(int statusCode, bool hasEarlyMedia)
    {
        if (hasEarlyMedia)
        {
            return null;
        }

        return statusCode switch
        {
            180 or 181 or 182 or 183 => ToneKind.Ringback,
            _ => null
        };
    }

    public static ToneKind FromAnswer() => ToneKind.Answer;

    public static ToneKind FromHangup() => ToneKind.Hangup;

    public static ToneKind FromFailure(int? statusCode, bool unreachable)
    {
        if (unreachable)
        {
            return ToneKind.Error;
        }

        return statusCode switch
        {
            486 or 600 => ToneKind.Busy,
            480 or 408 or 503 or 603 => ToneKind.Congestion,
            487 => ToneKind.Silence, // Request terminated (local cancel)
            _ => ToneKind.Error
        };
    }
}
