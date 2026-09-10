namespace QuickSIP.Core.Sip;

/// <summary>
/// RFC 4733 / RFC 2833 telephone-event IDs and keyboard mapping.
/// Numpad * → event 10 (*). Numpad / → event 11 (#).
/// </summary>
public static class DtmfMapper
{
    public const byte Star = 10;
    public const byte Pound = 11;

    public static bool TryFromChar(char c, out byte eventId)
    {
        eventId = 0;
        c = char.ToUpperInvariant(c);
        if (c is >= '0' and <= '9')
        {
            eventId = (byte)(c - '0');
            return true;
        }

        switch (c)
        {
            case '*':
                eventId = Star;
                return true;
            case '#':
                eventId = Pound;
                return true;
            case 'A':
                eventId = 12;
                return true;
            case 'B':
                eventId = 13;
                return true;
            case 'C':
                eventId = 14;
                return true;
            case 'D':
                eventId = 15;
                return true;
            default:
                return false;
        }
    }

    public static bool TryFromKeyName(string keyName, bool shift, out byte eventId)
    {
        eventId = 0;
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return false;
        }

        switch (keyName)
        {
            case "D0":
            case "NumPad0":
                eventId = 0;
                return true;
            case "D1":
            case "NumPad1":
                eventId = 1;
                return true;
            case "D2":
            case "NumPad2":
                eventId = 2;
                return true;
            case "D3":
            case "NumPad3":
                eventId = shift && keyName == "D3" ? Pound : (byte)3;
                return true;
            case "D4":
            case "NumPad4":
                eventId = 4;
                return true;
            case "D5":
            case "NumPad5":
                eventId = 5;
                return true;
            case "D6":
            case "NumPad6":
                eventId = 6;
                return true;
            case "D7":
            case "NumPad7":
                eventId = 7;
                return true;
            case "D8":
            case "NumPad8":
                eventId = shift && keyName == "D8" ? Star : (byte)8;
                return true;
            case "D9":
            case "NumPad9":
                eventId = 9;
                return true;
            case "Multiply":
            case "Oem8":
                eventId = Star;
                return true;
            case "Divide":
            case "Oem2":
            case "OemQuestion":
                eventId = Pound;
                return true;
            default:
                return TryFromChar(keyName.Length == 1 ? keyName[0] : '\0', out eventId);
        }
    }

    public static char ToChar(byte eventId) => eventId switch
    {
        <= 9 => (char)('0' + eventId),
        Star => '*',
        Pound => '#',
        12 => 'A',
        13 => 'B',
        14 => 'C',
        15 => 'D',
        _ => '?'
    };

    public static bool IsDtmfChar(char c) => TryFromChar(c, out _);
}
