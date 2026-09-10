using QuickSIP.Core.Models;

namespace QuickSIP.Core.Sip;

public static class SipAddressing
{
    public static string BuildRegistrarHost(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var host = (settings.SipServer ?? "").Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("SIP server is required.");
        }

        if (host.Contains("sip:", StringComparison.OrdinalIgnoreCase)
            || host.Contains("sips:", StringComparison.OrdinalIgnoreCase))
        {
            return host;
        }

        var transport = (settings.Transport ?? "udp").Trim().ToLowerInvariant();
        var port = settings.SipPort <= 0 ? 5060 : settings.SipPort;
        var scheme = transport == "tls" ? "sips" : "sip";
        var uri = $"{scheme}:{host}:{port}";
        if (transport is "tcp" or "tls" or "ws" or "wss")
        {
            uri += $";transport={transport}";
        }

        return uri;
    }

    public static string BuildInviteUri(string numberOrUri, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var target = (numberOrUri ?? "").Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("A number or SIP URI is required.", nameof(numberOrUri));
        }

        if (target.Contains("sip:", StringComparison.OrdinalIgnoreCase)
            || target.Contains("sips:", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        var host = settings.EffectiveDomain;
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("SIP domain or server is required to place a call.");
        }

        if (host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('[') && !IPLooksLikeIPv6(host))
        {
            // host:port — strip a scheme if the user put one in Domain.
            host = host.Replace("sip:", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("sips:", "", StringComparison.OrdinalIgnoreCase);
        }

        var transport = (settings.Transport ?? "udp").Trim().ToLowerInvariant();
        var uri = $"sip:{target}@{host}";
        if (transport is "tcp" or "tls")
        {
            uri += $";transport={transport}";
        }

        return uri;
    }

    public static string BuildFromHeader(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var user = string.IsNullOrWhiteSpace(settings.Username) ? "quicksip" : settings.Username.Trim();
        var host = string.IsNullOrWhiteSpace(settings.EffectiveDomain) ? "localhost" : settings.EffectiveDomain;
        var aor = $"sip:{user}@{host}";
        if (string.IsNullOrWhiteSpace(settings.DisplayName))
        {
            return aor;
        }

        var display = settings.DisplayName.Replace("\"", "'", StringComparison.Ordinal);
        return $"\"{display}\" <{aor}>";
    }

    public static string DescribeIncoming(string? fromHeader, string? fallbackUri)
    {
        if (!string.IsNullOrWhiteSpace(fromHeader))
        {
            var trimmed = fromHeader.Trim();
            var start = trimmed.IndexOf('<');
            var end = trimmed.IndexOf('>');
            if (start >= 0 && end > start)
            {
                var uri = trimmed[(start + 1)..end];
                var display = trimmed[..start].Trim().Trim('"');
                return string.IsNullOrWhiteSpace(display) ? uri : $"{display} ({uri})";
            }

            return trimmed;
        }

        return string.IsNullOrWhiteSpace(fallbackUri) ? "Unknown caller" : fallbackUri;
    }

    private static bool IPLooksLikeIPv6(string host) => host.Contains(':') && host.Count(c => c == ':') >= 2;
}
