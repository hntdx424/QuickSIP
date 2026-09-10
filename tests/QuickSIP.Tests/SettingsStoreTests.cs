using QuickSIP.Core.Models;
using QuickSIP.Core.Security;
using QuickSIP.Core.Settings;
using Xunit;

namespace QuickSIP.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void RoundTrip_PersistsFieldsAndProtectedPassword()
    {
        var dir = Path.Combine(Path.GetTempPath(), "quicksip-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir, new DpapiSecretProtector());
            var settings = SettingsStore.CreateDefault();
            settings.SipServer = "sip.example.com";
            settings.SipPort = 5060;
            settings.Username = "1001";
            settings.AuthUsername = "1001auth";
            settings.DisplayName = "Desk Phone";
            settings.Domain = "example.com";
            settings.Transport = "tcp";
            settings.InputDeviceIndex = 1;
            settings.OutputDeviceIndex = 2;
            settings.RegisterOnStartup = false;
            store.SetPassword(settings, "s3cret!");
            store.Save(settings);

            Assert.True(File.Exists(store.FilePath));
            var json = File.ReadAllText(store.FilePath);
            Assert.DoesNotContain("s3cret!", json);
            Assert.Contains("protectedPassword", json);

            var loaded = store.Load();
            Assert.Equal("sip.example.com", loaded.SipServer);
            Assert.Equal("1001", loaded.Username);
            Assert.Equal("1001auth", loaded.AuthUsername);
            Assert.Equal("tcp", loaded.Transport);
            Assert.Equal(1, loaded.InputDeviceIndex);
            Assert.Equal("s3cret!", store.GetPassword(loaded));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_MissingFile_CreatesDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "quicksip-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            var loaded = store.Load();
            Assert.True(File.Exists(store.FilePath));
            Assert.Equal(5060, loaded.SipPort);
            Assert.False(string.IsNullOrWhiteSpace(loaded.RecordingsFolder));
            Assert.Contains("QuickSIP", loaded.RecordingsFolder);
            Assert.Contains("Recordings", loaded.RecordingsFolder);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void DefaultDirectory_IsAppDataQuickSIP()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QuickSIP");
        Assert.Equal(expected, SettingsStore.DefaultDirectory);
        Assert.Equal("settings.json", SettingsStore.SettingsFileName);
    }

    [Fact]
    public void EffectiveAuthUsername_FallsBackToUsername()
    {
        var settings = new AppSettings { Username = "alice", AuthUsername = "  " };
        Assert.Equal("alice", settings.EffectiveAuthUsername);
        settings.AuthUsername = "bob";
        Assert.Equal("bob", settings.EffectiveAuthUsername);
    }

    [Fact]
    public void DpapiProtector_RoundTrip()
    {
        var protector = new DpapiSecretProtector();
        var wrapped = protector.Protect("hunter2");
        Assert.DoesNotContain("hunter2", wrapped);
        Assert.Equal("hunter2", protector.Unprotect(wrapped));
        if (!OperatingSystem.IsWindows())
        {
            Assert.StartsWith(DpapiSecretProtector.LocalPrefix, wrapped);
        }
        else
        {
            Assert.StartsWith(DpapiSecretProtector.DpapiPrefix, wrapped);
        }
    }
}
