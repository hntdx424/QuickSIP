using System.Windows;
using QuickSIP.Core.Settings;
using QuickSIP.Services;

namespace QuickSIP;

public partial class App : Application
{
    public SettingsStore SettingsStore { get; } = SettingsStore.ForCurrentUser();
    public SipPhoneService Phone { get; }

    public App()
    {
        Phone = new SipPhoneService(SettingsStore);
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.Message, "QuickSIP", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Phone.Dispose();
        base.OnExit(e);
    }
}
