using System.Text.Json;
using QuickSIP.Core.Models;
using QuickSIP.Core.Security;

namespace QuickSIP.Core.Settings;

public sealed class SettingsStore
{
    public const string SettingsFileName = "settings.json";
    public const string AppFolderName = "QuickSIP";
    public const string RecordingsFolderName = "Recordings";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ISecretProtector _protector;

    public SettingsStore(string directory, ISecretProtector? protector = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = directory;
        FilePath = Path.Combine(directory, SettingsFileName);
        _protector = protector ?? new DpapiSecretProtector();
    }

    public string DirectoryPath { get; }
    public string FilePath { get; }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    public static string DefaultRecordingsFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            AppFolderName,
            RecordingsFolderName);

    public static SettingsStore ForCurrentUser(ISecretProtector? protector = null) =>
        new(DefaultDirectory, protector);

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            var fresh = CreateDefault();
            Save(fresh);
            return fresh;
        }

        var json = File.ReadAllText(FilePath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefault();
        if (string.IsNullOrWhiteSpace(settings.RecordingsFolder))
        {
            settings.RecordingsFolder = DefaultRecordingsFolder;
        }

        if (settings.SipPort <= 0 || settings.SipPort > 65535)
        {
            settings.SipPort = AppSettings.DefaultSipPort;
        }

        if (settings.RegisterExpirySeconds <= 0)
        {
            settings.RegisterExpirySeconds = AppSettings.DefaultRegisterExpirySeconds;
        }

        if (string.IsNullOrWhiteSpace(settings.Transport))
        {
            settings.Transport = AppSettings.DefaultTransport;
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(DirectoryPath);
        if (string.IsNullOrWhiteSpace(settings.RecordingsFolder))
        {
            settings.RecordingsFolder = DefaultRecordingsFolder;
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    public string GetPassword(AppSettings settings) =>
        _protector.Unprotect(settings.ProtectedPassword);

    public void SetPassword(AppSettings settings, string? password)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ProtectedPassword = _protector.Protect(password ?? "");
    }

    public static AppSettings CreateDefault() => new()
    {
        RecordingsFolder = DefaultRecordingsFolder
    };
}
