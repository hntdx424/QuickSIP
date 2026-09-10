using System.Security.Cryptography;
using System.Text;

namespace QuickSIP.Core.Security;

/// <summary>
/// Protects secrets with Windows DPAPI (<see cref="DataProtectionScope.CurrentUser"/>).
/// Non-Windows runtimes (unit tests / Linux CI) use a reversible local encoding so
/// settings helpers remain testable; the Windows app always uses DPAPI.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    public const string DpapiPrefix = "dpapi:";
    public const string LocalPrefix = "local:";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("QuickSIP.v1.dpapi");

    public string Protect(string plaintext)
    {
        plaintext ??= "";
        if (OperatingSystem.IsWindows())
        {
            var bytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plaintext),
                Entropy,
                DataProtectionScope.CurrentUser);
            return DpapiPrefix + Convert.ToBase64String(bytes);
        }

        return LocalPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return "";
        }

        if (protectedValue.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI-protected secrets can only be read on Windows.");
            }

            var bytes = Convert.FromBase64String(protectedValue[DpapiPrefix.Length..]);
            var plain = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        if (protectedValue.StartsWith(LocalPrefix, StringComparison.Ordinal))
        {
            var bytes = Convert.FromBase64String(protectedValue[LocalPrefix.Length..]);
            return Encoding.UTF8.GetString(bytes);
        }

        // Legacy / unprefixed values: try DPAPI on Windows, otherwise treat as plain.
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var bytes = Convert.FromBase64String(protectedValue);
                var plain = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (FormatException)
            {
                return protectedValue;
            }
            catch (CryptographicException)
            {
                return protectedValue;
            }
        }

        return protectedValue;
    }
}
