namespace QuickSIP.Core.Sip;

public enum RegisterFailureKind
{
    None = 0,
    Unreachable,
    Authentication,
    NotFound,
    Timeout,
    ServerError,
    Other
}

public readonly record struct RegisterFailure(RegisterFailureKind Kind, string Banner);

/// <summary>
/// Maps SIP REGISTER outcomes to the bottom-of-window banner text.
/// Failed REGISTER is silent (no tone); only a banner is shown.
/// </summary>
public static class RegisterBannerMapper
{
    public static RegisterFailure Map(int? sipStatusCode, string? errorMessage = null)
    {
        if (IsUnreachable(sipStatusCode, errorMessage))
        {
            return new RegisterFailure(
                RegisterFailureKind.Unreachable,
                "Cannot reach the SIP server. Check the host, port, and network.");
        }

        if (sipStatusCode is 401 or 403 or 407)
        {
            return new RegisterFailure(
                RegisterFailureKind.Authentication,
                $"Registration failed: authentication rejected (SIP {sipStatusCode}). Check username and password.");
        }

        if (sipStatusCode == 404)
        {
            return new RegisterFailure(
                RegisterFailureKind.NotFound,
                "Registration failed: account not found (SIP 404).");
        }

        if (sipStatusCode is 408 or 504)
        {
            return new RegisterFailure(
                RegisterFailureKind.Timeout,
                $"Registration failed: SIP server timed out (SIP {sipStatusCode}).");
        }

        if (sipStatusCode is >= 500 and <= 599)
        {
            return new RegisterFailure(
                RegisterFailureKind.ServerError,
                $"Registration failed: SIP server error (SIP {sipStatusCode}).");
        }

        if (sipStatusCode is null)
        {
            var detail = string.IsNullOrWhiteSpace(errorMessage) ? "unknown error" : errorMessage.Trim();
            return new RegisterFailure(
                RegisterFailureKind.Other,
                $"Registration failed: {detail}.");
        }

        return new RegisterFailure(
            RegisterFailureKind.Other,
            $"Registration failed (SIP {sipStatusCode}).");
    }

    public static bool IsUnreachable(int? sipStatusCode, string? errorMessage)
    {
        if (sipStatusCode is not null and not 408 and not 503)
        {
            // 408/503 can still be unreachable depending on the message.
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return false;
            }
        }

        if (sipStatusCode is null or 408 or 503)
        {
            if (LooksLikeNetworkFailure(errorMessage))
            {
                return true;
            }

            if (sipStatusCode is null)
            {
                return string.IsNullOrWhiteSpace(errorMessage) || LooksLikeNetworkFailure(errorMessage);
            }
        }

        return LooksLikeNetworkFailure(errorMessage);
    }

    private static bool LooksLikeNetworkFailure(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        var msg = errorMessage.ToLowerInvariant();
        return msg.Contains("unreachable", StringComparison.Ordinal)
               || msg.Contains("timed out", StringComparison.Ordinal)
               || msg.Contains("timeout", StringComparison.Ordinal)
               || msg.Contains("time out", StringComparison.Ordinal)
               || msg.Contains("name resolution", StringComparison.Ordinal)
               || msg.Contains("dns", StringComparison.Ordinal)
               || msg.Contains("could not", StringComparison.Ordinal)
               || msg.Contains("unable to", StringComparison.Ordinal)
               || msg.Contains("connection", StringComparison.Ordinal)
               || msg.Contains("socket", StringComparison.Ordinal)
               || msg.Contains("no response", StringComparison.Ordinal)
               || msg.Contains("network", StringComparison.Ordinal)
               || msg.Contains("host not found", StringComparison.Ordinal)
               || msg.Contains("actively refused", StringComparison.Ordinal);
    }
}
