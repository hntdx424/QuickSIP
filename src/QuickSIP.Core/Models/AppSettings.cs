namespace QuickSIP.Core.Models;

/// <summary>
/// Persisted application settings. The SIP password is stored separately as a
/// DPAPI-protected blob (<see cref="ProtectedPassword"/>).
/// </summary>
public sealed class AppSettings
{
    public const string DefaultTransport = "udp";
    public const int DefaultSipPort = 5060;
    public const int DefaultRegisterExpirySeconds = 300;

    public string SipServer { get; set; } = "";
    public int SipPort { get; set; } = DefaultSipPort;
    public string Username { get; set; } = "";
    public string AuthUsername { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Domain { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    public string Transport { get; set; } = DefaultTransport;
    public int RegisterExpirySeconds { get; set; } = DefaultRegisterExpirySeconds;
    public bool RegisterOnStartup { get; set; } = true;
    public int LocalSipPort { get; set; }
    public int InputDeviceIndex { get; set; } = -1;
    public int OutputDeviceIndex { get; set; } = -1;
    public string RecordingsFolder { get; set; } = "";
    public bool EnableShortcuts { get; set; } = true;

    public string EffectiveDomain =>
        string.IsNullOrWhiteSpace(Domain) ? SipServer : Domain.Trim();

    public string EffectiveAuthUsername =>
        string.IsNullOrWhiteSpace(AuthUsername) ? Username : AuthUsername.Trim();

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
