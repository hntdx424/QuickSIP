using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QuickSIP.Core.Settings;
using QuickSIP.Core.Shortcuts;
using QuickSIP.Services;

namespace QuickSIP;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private bool _passwordTouched;

    public SettingsWindow()
    {
        InitializeComponent();
        _store = ((App)Application.Current).SettingsStore;
        var settings = _store.Load();

        SipServerBox.Text = settings.SipServer;
        SipPortBox.Text = settings.SipPort.ToString();
        SelectTransport(settings.Transport);
        UsernameBox.Text = settings.Username;
        AuthUsernameBox.Text = settings.AuthUsername;
        if (!string.IsNullOrEmpty(settings.ProtectedPassword))
        {
            PasswordBox.Password = "********";
        }

        PasswordBox.PasswordChanged += (_, _) => _passwordTouched = true;
        DisplayNameBox.Text = settings.DisplayName;
        DomainBox.Text = settings.Domain;
        ExpiryBox.Text = settings.RegisterExpirySeconds.ToString();
        LocalPortBox.Text = settings.LocalSipPort.ToString();
        RegisterOnStartupBox.IsChecked = settings.RegisterOnStartup;
        EnableShortcutsBox.IsChecked = settings.EnableShortcuts;
        RecordingsBox.Text = string.IsNullOrWhiteSpace(settings.RecordingsFolder)
            ? SettingsStore.DefaultRecordingsFolder
            : settings.RecordingsFolder;

        InputDeviceBox.ItemsSource = AudioDeviceEnumerator.Inputs();
        OutputDeviceBox.ItemsSource = AudioDeviceEnumerator.Outputs();
        SelectDevice(InputDeviceBox, settings.InputDeviceIndex);
        SelectDevice(OutputDeviceBox, settings.OutputDeviceIndex);
        ShortcutList.ItemsSource = ShortcutMap.Bindings;
    }

    private void SelectTransport(string transport)
    {
        var want = (transport ?? "udp").Trim().ToLowerInvariant();
        foreach (ComboBoxItem item in TransportBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), want, StringComparison.OrdinalIgnoreCase))
            {
                TransportBox.SelectedItem = item;
                return;
            }
        }

        TransportBox.SelectedIndex = 0;
    }

    private static void SelectDevice(ComboBox box, int index)
    {
        foreach (AudioDeviceOption option in box.Items)
        {
            if (option.Index == index)
            {
                box.SelectedItem = option;
                return;
            }
        }

        if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }

    private void OnBrowseRecordings(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Recordings folder",
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(RecordingsBox.Text) && Directory.Exists(RecordingsBox.Text))
        {
            dialog.InitialDirectory = RecordingsBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            RecordingsBox.Text = dialog.FolderName;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var settings = _store.Load();
        settings.SipServer = SipServerBox.Text.Trim();
        settings.SipPort = int.TryParse(SipPortBox.Text, out var port) ? Math.Clamp(port, 1, 65535) : 5060;
        settings.Transport = (TransportBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "udp";
        settings.Username = UsernameBox.Text.Trim();
        settings.AuthUsername = AuthUsernameBox.Text.Trim();
        settings.DisplayName = DisplayNameBox.Text.Trim();
        settings.Domain = DomainBox.Text.Trim();
        settings.RegisterExpirySeconds = int.TryParse(ExpiryBox.Text, out var expiry) ? Math.Clamp(expiry, 30, 3600) : 300;
        settings.LocalSipPort = int.TryParse(LocalPortBox.Text, out var local) ? Math.Clamp(local, 0, 65535) : 0;
        settings.RegisterOnStartup = RegisterOnStartupBox.IsChecked == true;
        settings.EnableShortcuts = EnableShortcutsBox.IsChecked == true;
        settings.RecordingsFolder = string.IsNullOrWhiteSpace(RecordingsBox.Text)
            ? SettingsStore.DefaultRecordingsFolder
            : RecordingsBox.Text.Trim();
        settings.InputDeviceIndex = (InputDeviceBox.SelectedItem as AudioDeviceOption)?.Index ?? -1;
        settings.OutputDeviceIndex = (OutputDeviceBox.SelectedItem as AudioDeviceOption)?.Index ?? -1;

        if (_passwordTouched)
        {
            var entered = PasswordBox.Password;
            if (entered != "********")
            {
                _store.SetPassword(settings, entered);
            }
        }

        _store.Save(settings);
        DialogResult = true;
    }
}
