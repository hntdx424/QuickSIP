namespace QuickSIP.Core.Shortcuts;

public enum PhoneAction
{
    Dial,
    Answer,
    Hangup,
    Hold,
    Mute,
    BlindTransfer,
    AttendedTransfer,
    Record,
    Register
}

public readonly record struct ShortcutBinding(
    PhoneAction Action,
    bool Ctrl,
    bool Shift,
    bool Alt,
    string KeyName,
    string Display,
    string Description);

public static class ShortcutMap
{
    public static readonly IReadOnlyList<ShortcutBinding> Bindings =
    [
        new(PhoneAction.Dial, true, true, false, "D", "Ctrl+Shift+D", "Dial the number in the display"),
        new(PhoneAction.Answer, true, true, false, "A", "Ctrl+Shift+A", "Answer an incoming call"),
        new(PhoneAction.Hangup, true, true, false, "H", "Ctrl+Shift+H", "Reject incoming or hang up"),
        new(PhoneAction.Hold, true, true, false, "O", "Ctrl+Shift+O", "Hold / resume"),
        new(PhoneAction.Mute, true, true, false, "M", "Ctrl+Shift+M", "Mute / unmute microphone"),
        new(PhoneAction.BlindTransfer, true, true, false, "B", "Ctrl+Shift+B", "Blind transfer (SIP REFER)"),
        new(PhoneAction.AttendedTransfer, true, true, false, "T", "Ctrl+Shift+T", "Start or complete attended transfer"),
        new(PhoneAction.Record, true, true, false, "C", "Ctrl+Shift+C", "Start / stop stereo recording"),
        new(PhoneAction.Register, true, true, false, "R", "Ctrl+Shift+R", "Register with the SIP server")
    ];

    public static bool TryMatch(bool ctrl, bool shift, bool alt, string keyName, out PhoneAction action)
    {
        action = default;
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return false;
        }

        var key = NormalizeKey(keyName);
        foreach (var binding in Bindings)
        {
            if (binding.Ctrl == ctrl
                && binding.Shift == shift
                && binding.Alt == alt
                && string.Equals(binding.KeyName, key, StringComparison.OrdinalIgnoreCase))
            {
                action = binding.Action;
                return true;
            }
        }

        return false;
    }

    public static bool IsEscapeHangup(bool ctrl, bool shift, bool alt, string keyName) =>
        !ctrl && !shift && !alt && NormalizeKey(keyName) is "Escape" or "Esc";

    public static bool IsEnterDial(bool ctrl, bool shift, bool alt, string keyName) =>
        !ctrl && !shift && !alt && NormalizeKey(keyName) is "Enter" or "Return";

    private static string NormalizeKey(string keyName)
    {
        var key = keyName.Trim();
        if (key.Length == 2 && key[0] == 'D' && char.IsLetter(key[1]))
        {
            return key[1].ToString();
        }

        return key;
    }
}
