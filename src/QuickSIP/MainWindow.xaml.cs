using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickSIP.Core.Shortcuts;
using QuickSIP.Core.Sip;
using QuickSIP.Services;

namespace QuickSIP;

public partial class MainWindow : Window
{
    private readonly SipPhoneService _phone;
    private bool _suppressDigitEcho;

    public MainWindow()
    {
        InitializeComponent();
        _phone = ((App)Application.Current).Phone;
        _phone.StateChanged += state => Dispatcher.Invoke(() => ApplyState(state));
        _phone.IncomingStarted += () => Dispatcher.Invoke(() =>
        {
            IncomingBar.Visibility = Visibility.Visible;
            TaskbarFlasher.Start(this);
            Activate();
        });
        _phone.IncomingCleared += () => Dispatcher.Invoke(() =>
        {
            IncomingBar.Visibility = Visibility.Collapsed;
            TaskbarFlasher.Stop(this);
        });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyState(_phone.Snapshot());
        if (_phone.Settings.RegisterOnStartup
            && !string.IsNullOrWhiteSpace(_phone.Settings.SipServer)
            && !string.IsNullOrWhiteSpace(_phone.Settings.Username))
        {
            await _phone.RegisterAsync();
        }
        else if (string.IsNullOrWhiteSpace(_phone.Settings.SipServer))
        {
            StatusText.Text = "Open Settings to configure your SIP account.";
        }
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        TaskbarFlasher.Stop(this);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var settings = _phone.Settings;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        var keyName = e.Key == Key.System ? e.SystemKey.ToString() : e.Key.ToString();

        if (ShortcutMap.IsEscapeHangup(ctrl, shift, alt, keyName))
        {
            _phone.Hangup();
            e.Handled = true;
            return;
        }

        if (ShortcutMap.IsEnterDial(ctrl, shift, alt, keyName))
        {
            await _phone.DialAsync(NumberBox.Text);
            e.Handled = true;
            return;
        }

        if (settings.EnableShortcuts && ShortcutMap.TryMatch(ctrl, shift, alt, keyName, out var action))
        {
            await RunShortcutAsync(action);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Multiply || e.Key == Key.Divide)
        {
            char c = e.Key == Key.Multiply ? '*' : '#';
            await AppendAndMaybeDtmfAsync(c);
            e.Handled = true;
            return;
        }

        if (DtmfMapper.TryFromKeyName(keyName, shift, out var eventId)
            && (e.Key is >= Key.NumPad0 and <= Key.NumPad9 || shift && e.Key is Key.D3 or Key.D8))
        {
            await AppendAndMaybeDtmfAsync(DtmfMapper.ToChar(eventId));
            e.Handled = true;
        }
    }

    private async Task RunShortcutAsync(PhoneAction action)
    {
        switch (action)
        {
            case PhoneAction.Dial:
                await _phone.DialAsync(NumberBox.Text);
                break;
            case PhoneAction.Answer:
                await _phone.AnswerAsync();
                break;
            case PhoneAction.Hangup:
                _phone.Hangup();
                break;
            case PhoneAction.Hold:
                _phone.ToggleHold();
                break;
            case PhoneAction.Mute:
                await _phone.ToggleMuteAsync();
                break;
            case PhoneAction.BlindTransfer:
                await _phone.BlindTransferAsync(NumberBox.Text);
                break;
            case PhoneAction.AttendedTransfer:
                await _phone.AttendedTransferAsync(NumberBox.Text);
                break;
            case PhoneAction.Record:
                _phone.ToggleRecording();
                break;
            case PhoneAction.Register:
                await _phone.RegisterAsync();
                break;
        }
    }

    private async void OnDigit(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var label = DigitFromButton(button);
        if (label is null)
        {
            return;
        }

        await AppendAndMaybeDtmfAsync(label.Value);
    }

    private static char? DigitFromButton(Button button)
    {
        if (button.Content is string s && s.Length > 0)
        {
            return s[0];
        }

        if (button.Content is StackPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is TextBlock tb && tb.Text.Length == 1 && DtmfMapper.IsDtmfChar(tb.Text[0]))
                {
                    return tb.Text[0];
                }
            }
        }

        return null;
    }

    private async Task AppendAndMaybeDtmfAsync(char c)
    {
        _suppressDigitEcho = true;
        NumberBox.Text += c;
        NumberBox.CaretIndex = NumberBox.Text.Length;
        _suppressDigitEcho = false;
        _phone.NotifyDigits(NumberBox.Text);
        if (_phone.IsCallActive && DtmfMapper.TryFromChar(c, out var id))
        {
            await _phone.SendDtmfAsync(id);
        }
    }

    private void OnNumberChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressDigitEcho)
        {
            return;
        }

        _phone.NotifyDigits(NumberBox.Text);
    }

    private void OnNumberGotFocus(object sender, RoutedEventArgs e)
    {
        NumberBox.CaretIndex = NumberBox.Text.Length;
    }

    private void OnBackspace(object sender, RoutedEventArgs e)
    {
        if (NumberBox.Text.Length == 0)
        {
            return;
        }

        NumberBox.Text = NumberBox.Text[..^1];
        NumberBox.CaretIndex = NumberBox.Text.Length;
        _phone.NotifyDigits(NumberBox.Text);
    }

    private async void OnDial(object sender, RoutedEventArgs e) => await _phone.DialAsync(NumberBox.Text);
    private void OnHangup(object sender, RoutedEventArgs e) => _phone.Hangup();
    private async void OnAnswer(object sender, RoutedEventArgs e) => await _phone.AnswerAsync();
    private void OnReject(object sender, RoutedEventArgs e) => _phone.Reject();
    private void OnHold(object sender, RoutedEventArgs e) => _phone.ToggleHold();
    private async void OnMute(object sender, RoutedEventArgs e) => await _phone.ToggleMuteAsync();
    private async void OnBlindTransfer(object sender, RoutedEventArgs e) => await _phone.BlindTransferAsync(NumberBox.Text);
    private async void OnAttendedTransfer(object sender, RoutedEventArgs e) => await _phone.AttendedTransferAsync(NumberBox.Text);
    private void OnRecord(object sender, RoutedEventArgs e) => _phone.ToggleRecording();
    private async void OnRegister(object sender, RoutedEventArgs e) => await _phone.RegisterAsync();
    private async void OnUnregister(object sender, RoutedEventArgs e) => await _phone.UnregisterAsync();

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow { Owner = this };
        window.ShowDialog();
        _phone.ReloadSettings();
        ApplyState(_phone.Snapshot());
    }

    private void ApplyState(PhoneState state)
    {
        StatusText.Text = state.Status;
        IncomingFromText.Text = state.IncomingFrom ?? "";
        IncomingBar.Visibility = string.IsNullOrEmpty(state.IncomingFrom) ? Visibility.Collapsed : Visibility.Visible;
        BannerText.Text = state.Banner ?? "";
        BannerBar.Visibility = string.IsNullOrEmpty(state.Banner) ? Visibility.Collapsed : Visibility.Visible;
        MuteButton.Content = state.IsMuted ? "Unmute" : "Mute";
        HoldButton.Content = state.IsHeld ? "Resume" : "Hold";
        RecordButton.Content = state.IsRecording ? "Stop recording" : "Record";
        RegDot.Fill = state.IsRegistered
            ? (Brush)FindResource("GreenBrush")
            : state.Phase == PhonePhase.Registering
                ? (Brush)FindResource("AmberBrush")
                : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    }
}
