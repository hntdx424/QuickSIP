using System.IO;
using System.Net;
using QuickSIP.Core.Models;
using QuickSIP.Core.Recording;
using QuickSIP.Core.Settings;
using QuickSIP.Core.Sip;
using QuickSIP.Core.Tones;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;

namespace QuickSIP.Services;

public enum PhonePhase
{
    Idle,
    Registering,
    Incoming,
    Dialing,
    InCall,
    Held
}

public sealed class PhoneState
{
    public PhonePhase Phase { get; init; }
    public bool IsRegistered { get; init; }
    public bool IsMuted { get; init; }
    public bool IsHeld { get; init; }
    public bool IsRecording { get; init; }
    public bool AttendedTransferPending { get; init; }
    public string Status { get; init; } = "Idle";
    public string? Banner { get; init; }
    public string? RemoteParty { get; init; }
    public string? IncomingFrom { get; init; }
    public string? RecordingPath { get; init; }
}

public sealed class SipPhoneService : IDisposable
{
    private readonly SettingsStore _store;
    private readonly ITonePlayer _tones;
    private readonly object _gate = new();
    private readonly AudioEncoder _encoder = new();

    private AppSettings _settings;
    private SIPTransport? _transport;
    private SIPRegistrationUserAgent? _reg;
    private SIPUserAgent? _ua;
    private SIPUserAgent? _uaConsult;
    private SIPServerUserAgent? _pendingUas;
    private WindowsAudioEndPoint? _audio;
    private VoIPMediaSession? _session;
    private StereoCallRecorder? _recorder;
    private AudioFormat _negotiatedFormat;
    private bool _haveNegotiatedFormat;
    private bool _registered;
    private bool _muted;
    private bool _held;
    private bool _attendedPending;
    private bool _disposed;
    private bool _expectLocalHangup;
    private string? _banner;
    private string _status = "Unregistered";
    private string? _remoteParty;
    private string? _incomingFrom;
    private string _digits = "";
    private string? _recordingPath;

    public SipPhoneService(SettingsStore store, ITonePlayer? tones = null)
    {
        _store = store;
        _tones = tones ?? new TonePlayer();
        _settings = store.Load();
    }

    public event Action<PhoneState>? StateChanged;
    public event Action? IncomingStarted;
    public event Action? IncomingCleared;

    public AppSettings Settings => _settings;
    public bool IsRegistered => _registered;
    public bool IsCallActive => _ua?.IsCallActive == true || _uaConsult?.IsCallActive == true;
    public bool HasIncoming => _pendingUas is not null;

    public void ReloadSettings()
    {
        _settings = _store.Load();
        Publish();
    }

    public async Task RegisterAsync()
    {
        ThrowIfDisposed();
        _settings = _store.Load();
        if (string.IsNullOrWhiteSpace(_settings.SipServer) || string.IsNullOrWhiteSpace(_settings.Username))
        {
            _banner = "Set SIP server and username in Settings before registering.";
            _status = "Not configured";
            _registered = false;
            Publish();
            return;
        }

        await TeardownSipAsync(unregister: true);
        _banner = null;
        _status = "Registering…";
        Publish();

        try
        {
            EnsureTransport();
            EnsureUserAgent();
            var password = _store.GetPassword(_settings);
            var host = SipAddressing.BuildRegistrarHost(_settings);
            var expiry = Math.Clamp(_settings.RegisterExpirySeconds, 30, 3600);
            _reg = CreateRegistrar(host, password, expiry);
            _reg.RegistrationSuccessful += OnRegistrationSuccessful;
            _reg.RegistrationFailed += OnRegistrationFailed;
            _reg.RegistrationTemporaryFailure += OnRegistrationFailed;
            _reg.RegistrationRemoved += (_, _) =>
            {
                _registered = false;
                _status = "Unregistered";
                _tones.Stop();
                Publish();
            };
            _reg.Start();
        }
        catch (Exception ex)
        {
            var failure = RegisterBannerMapper.Map(null, ex.Message);
            _banner = failure.Banner;
            _status = "Registration failed";
            _registered = false;
            _tones.Stop();
            Publish();
        }
    }

    public async Task UnregisterAsync()
    {
        _registered = false;
        _banner = null;
        _status = "Unregistered";
        _tones.Stop();
        Publish();
        await TeardownSipAsync(unregister: true);
        Publish();
    }

    public async Task DialAsync(string number)
    {
        ThrowIfDisposed();
        number = number.Trim();
        if (string.IsNullOrWhiteSpace(number))
        {
            return;
        }

        if (_attendedPending && _ua?.IsCallActive == true && _uaConsult is null)
        {
            await PlaceConsultCallAsync(number);
            return;
        }

        if (_ua?.IsCallActive == true || _pendingUas is not null)
        {
            return;
        }

        EnsureTransport();
        EnsureUserAgent();
        _settings = _store.Load();
        var dst = SipAddressing.BuildInviteUri(number, _settings);
        var password = _store.GetPassword(_settings);
        var media = CreateMediaSession();
        var descriptor = new SIPCallDescriptor(
            string.IsNullOrWhiteSpace(_settings.Username) ? "quicksip" : _settings.Username,
            password,
            dst,
            SipAddressing.BuildFromHeader(_settings),
            dst,
            null,
            null,
            _settings.EffectiveAuthUsername,
            SIPCallDirection.Out,
            null,
            null,
            null);

        _remoteParty = dst;
        _status = $"Calling {number}…";
        _banner = null;
        _tones.Stop();
        Publish();
        await _ua!.InitiateCallAsync(descriptor, media);
    }

    public async Task AnswerAsync()
    {
        var uas = _pendingUas;
        if (uas is null || _ua is null)
        {
            return;
        }

        _tones.Stop();
        IncomingCleared?.Invoke();
        try
        {
            var media = CreateMediaSession();
            _remoteParty = _incomingFrom;
            _pendingUas = null;
            _incomingFrom = null;
            _status = "Connecting…";
            Publish();
            var ok = await _ua.Answer(uas, media);
            if (ok)
            {
                _status = $"In call with {_remoteParty}";
                _tones.Play(ProgressToneMapper.FromAnswer());
            }
            else
            {
                _status = "Answer failed (codec or media)";
                _tones.Play(ToneKind.Error);
                CleanupMedia();
            }
        }
        catch (Exception ex)
        {
            _status = $"Answer failed: {ex.Message}";
            _tones.Play(ToneKind.Error);
        }

        Publish();
    }

    public void Reject()
    {
        var uas = _pendingUas;
        _pendingUas = null;
        _incomingFrom = null;
        _tones.Stop();
        IncomingCleared?.Invoke();
        try
        {
            uas?.Reject(SIPResponseStatusCodesEnum.BusyHere, "Rejected");
        }
        catch
        {
            // Already gone.
        }

        _status = _registered ? "Registered" : "Idle";
        MaybeDialTone();
        Publish();
    }

    public void Hangup()
    {
        if (_pendingUas is not null)
        {
            Reject();
            return;
        }

        _expectLocalHangup = true;
        StopRecording();
        try
        {
            if (_uaConsult?.IsCallActive == true)
            {
                _uaConsult.Hangup();
            }
            else if (_ua?.IsCalling == true || _ua?.IsRinging == true)
            {
                _ua.Cancel();
            }
            else
            {
                _ua?.Hangup();
            }
        }
        catch
        {
            // Ignore.
        }

        _attendedPending = false;
        _held = false;
        _muted = false;
        _remoteParty = null;
        CleanupMedia();
        _status = _registered ? "Registered" : "Idle";
        _tones.Play(ProgressToneMapper.FromHangup());
        _ = Task.Delay(500).ContinueWith(__ =>
        {
            _expectLocalHangup = false;
            MaybeDialTone();
            Publish();
        });
        Publish();
    }

    public void ToggleHold()
    {
        if (_ua is not { IsCallActive: true })
        {
            return;
        }

        try
        {
            if (_held || _ua.IsOnLocalHold)
            {
                _ua.TakeOffHold();
                _held = false;
                _status = $"In call with {_remoteParty}";
            }
            else
            {
                _ua.PutOnHold();
                _held = true;
                _status = "On hold";
            }
        }
        catch (Exception ex)
        {
            _status = $"Hold failed: {ex.Message}";
        }

        Publish();
    }

    public async Task ToggleMuteAsync()
    {
        if (_audio is null || _ua is not { IsCallActive: true })
        {
            return;
        }

        try
        {
            if (_muted)
            {
                await _audio.ResumeAudio();
                _muted = false;
            }
            else
            {
                await _audio.PauseAudio();
                _muted = true;
            }

            _status = _muted ? "Muted" : (_held ? "On hold" : $"In call with {_remoteParty}");
        }
        catch (Exception ex)
        {
            _status = $"Mute failed: {ex.Message}";
        }

        Publish();
    }

    public async Task SendDtmfAsync(byte eventId)
    {
        if (_uaConsult is { IsCallActive: true })
        {
            await _uaConsult.SendDtmf(eventId);
            return;
        }

        if (_ua is { IsCallActive: true })
        {
            await _ua.SendDtmf(eventId);
        }
    }

    public async Task BlindTransferAsync(string target)
    {
        if (_ua is not { IsCallActive: true })
        {
            _status = "Blind transfer requires an active call.";
            Publish();
            return;
        }

        target = target.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            _status = "Enter a number, then press Blind Transfer.";
            Publish();
            return;
        }

        try
        {
            var uri = SIPURI.ParseSIPURIRelaxed(SipAddressing.BuildInviteUri(target, _settings));
            _status = $"Blind transfer to {target}…";
            Publish();
            var ok = await _ua.BlindTransfer(uri, TimeSpan.FromSeconds(20), CancellationToken.None);
            _status = ok ? "Transfer accepted" : "Transfer failed";
        }
        catch (Exception ex)
        {
            _status = $"Transfer failed: {ex.Message}";
            _tones.Play(ToneKind.Error);
        }

        Publish();
    }

    public async Task AttendedTransferAsync(string number)
    {
        if (_uaConsult is { IsCallActive: true } && _ua is { IsCallActive: true })
        {
            try
            {
                _status = "Completing attended transfer…";
                Publish();
                var ok = await _uaConsult.AttendedTransfer(_ua.Dialogue, TimeSpan.FromSeconds(20), CancellationToken.None);
                _attendedPending = false;
                _status = ok ? "Attended transfer accepted" : "Attended transfer failed";
            }
            catch (Exception ex)
            {
                _status = $"Attended transfer failed: {ex.Message}";
                _tones.Play(ToneKind.Error);
            }

            Publish();
            return;
        }

        if (_ua is not { IsCallActive: true })
        {
            _status = "Attended transfer requires an active call.";
            Publish();
            return;
        }

        if (!_attendedPending)
        {
            try
            {
                if (!_held)
                {
                    _ua.PutOnHold();
                    _held = true;
                }
            }
            catch
            {
                // Continue; consult call can still proceed.
            }

            _attendedPending = true;
            number = number.Trim();
            if (!string.IsNullOrWhiteSpace(number))
            {
                await PlaceConsultCallAsync(number);
            }
            else
            {
                _status = "Attended transfer: enter number and Dial (or press T again).";
                Publish();
            }

            return;
        }

        number = number.Trim();
        if (!string.IsNullOrWhiteSpace(number) && _uaConsult is null)
        {
            await PlaceConsultCallAsync(number);
        }
    }

    public void ToggleRecording()
    {
        if (_recorder is not null)
        {
            StopRecording();
            _status = "Recording saved";
            Publish();
            return;
        }

        if (_ua is not { IsCallActive: true } && _uaConsult is not { IsCallActive: true })
        {
            _status = "Start a call before recording.";
            Publish();
            return;
        }

        try
        {
            var folder = string.IsNullOrWhiteSpace(_settings.RecordingsFolder)
                ? SettingsStore.DefaultRecordingsFolder
                : _settings.RecordingsFolder;
            Directory.CreateDirectory(folder);
            var name = $"quicksip-{DateTime.Now:yyyyMMdd-HHmmss}.wav";
            _recordingPath = Path.Combine(folder, name);
            _recorder = new StereoCallRecorder(_recordingPath);
            _status = $"Recording {name}";
        }
        catch (Exception ex)
        {
            _status = $"Recording failed: {ex.Message}";
            _recorder = null;
            _recordingPath = null;
        }

        Publish();
    }

    public void NotifyDigits(string digits)
    {
        _digits = digits ?? "";
        if (_ua?.IsCallActive == true || _pendingUas is not null || !_registered)
        {
            return;
        }

        if (_digits.Length == 0)
        {
            MaybeDialTone();
        }
        else if (_tones.Current == ToneKind.Dial)
        {
            _tones.Stop();
        }
    }

    public PhoneState Snapshot() => new()
    {
        Phase = CurrentPhase(),
        IsRegistered = _registered,
        IsMuted = _muted,
        IsHeld = _held,
        IsRecording = _recorder is not null,
        AttendedTransferPending = _attendedPending,
        Status = _status,
        Banner = _banner,
        RemoteParty = _remoteParty,
        IncomingFrom = _incomingFrom,
        RecordingPath = _recordingPath
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Hangup();
        }
        catch
        {
            // Ignore.
        }

        TeardownSipAsync(unregister: true).GetAwaiter().GetResult();
        _tones.Dispose();
    }

    private PhonePhase CurrentPhase()
    {
        if (_pendingUas is not null)
        {
            return PhonePhase.Incoming;
        }

        if (_held)
        {
            return PhonePhase.Held;
        }

        if (_ua?.IsCallActive == true || _uaConsult?.IsCallActive == true)
        {
            return PhonePhase.InCall;
        }

        if (_ua?.IsCalling == true || _ua?.IsRinging == true)
        {
            return PhonePhase.Dialing;
        }

        if (!_registered && _reg is not null && string.Equals(_status, "Registering…", StringComparison.Ordinal))
        {
            return PhonePhase.Registering;
        }

        return PhonePhase.Idle;
    }

    private void EnsureTransport()
    {
        if (_transport is not null)
        {
            return;
        }

        _transport = new SIPTransport();
        int port = _settings.LocalSipPort > 0 && _settings.LocalSipPort < 65536 ? _settings.LocalSipPort : 0;
        var local = new IPEndPoint(IPAddress.Any, port);
        var transport = (_settings.Transport ?? "udp").Trim().ToLowerInvariant();
        if (transport == "tcp")
        {
            _transport.AddSIPChannel(new SIPTCPChannel(local));
        }
        else
        {
            _transport.AddSIPChannel(new SIPUDPChannel(local));
        }

        _transport.SIPTransportRequestReceived += OnTransportRequest;
    }

    private Task OnTransportRequest(SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request)
    {
        if (request.Method == SIPMethodsEnum.OPTIONS)
        {
            _ = _transport?.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null));
        }

        return Task.CompletedTask;
    }

    private void EnsureUserAgent()
    {
        if (_ua is not null)
        {
            return;
        }

        _ua = new SIPUserAgent(_transport, null, isTransportExclusive: false);
        _ua.OnIncomingCall += OnIncomingCall;
        _ua.ClientCallTrying += (_, resp) =>
        {
            _status = $"Trying ({(int)resp.Status} {resp.ReasonPhrase})";
            Publish();
        };
        _ua.ClientCallRinging += (_, resp) =>
        {
            bool early = resp.Status == SIPResponseStatusCodesEnum.SessionProgress && !string.IsNullOrEmpty(resp.Body);
            var tone = ProgressToneMapper.FromProvisional((int)resp.Status, early);
            if (tone is not null)
            {
                _tones.Play(tone.Value);
            }
            else if (early)
            {
                _tones.Stop();
            }

            _status = $"Ringing ({(int)resp.Status})";
            Publish();
        };
        _ua.ClientCallFailed += (uac, err, resp) =>
        {
            int? code = resp is null ? null : (int)resp.Status;
            var tone = ProgressToneMapper.FromFailure(code, resp is null);
            if (tone != ToneKind.Silence)
            {
                _tones.Play(tone);
            }

            _status = resp is null ? $"Call failed: {err}" : $"Call failed ({(int)resp.Status} {resp.ReasonPhrase})";
            _remoteParty = null;
            CleanupMedia();
            Publish();
            _ = Task.Delay(1200).ContinueWith(__ =>
            {
                MaybeDialTone();
                Publish();
            });
        };
        _ua.ClientCallAnswered += (_, resp) =>
        {
            if (resp.Status == SIPResponseStatusCodesEnum.Ok)
            {
                _tones.Play(ProgressToneMapper.FromAnswer());
                _status = $"In call with {_remoteParty}";
            }
            else
            {
                _status = $"Call ended ({(int)resp.Status})";
            }

            Publish();
        };
        _ua.OnCallHungup += _ => HandleRemoteHangup(consult: false);
        _ua.ServerCallCancelled += (_, _) =>
        {
            _pendingUas = null;
            _incomingFrom = null;
            _tones.Stop();
            IncomingCleared?.Invoke();
            _status = _registered ? "Registered" : "Idle";
            MaybeDialTone();
            Publish();
        };
        _ua.OnDtmfTone += (id, duration) =>
        {
            _status = $"DTMF {DtmfMapper.ToChar(id)} ({duration} ms)";
            Publish();
        };
    }

    private SIPRegistrationUserAgent CreateRegistrar(string host, string password, int expiry)
    {
        var user = _settings.Username.Trim();
        var auth = _settings.EffectiveAuthUsername;
        if (string.Equals(user, auth, StringComparison.Ordinal))
        {
            return new SIPRegistrationUserAgent(_transport, user, password, host, expiry);
        }

        var aor = SIPURI.ParseSIPURIRelaxed($"sip:{user}@{_settings.EffectiveDomain}");
        return new SIPRegistrationUserAgent(
            _transport,
            outboundProxy: null,
            sipAccountAOR: aor,
            authUsername: auth,
            password: password,
            realm: _settings.EffectiveDomain,
            registrarHost: host,
            contactURI: aor,
            expiry: expiry,
            customHeaders: null);
    }

    private void OnRegistrationSuccessful(SIPURI uri, SIPResponse response)
    {
        _registered = true;
        _banner = null;
        _status = $"Registered as {uri}";
        MaybeDialTone();
        Publish();
    }

    private void OnRegistrationFailed(SIPURI uri, SIPResponse? response, string error)
    {
        _registered = false;
        int? code = response is null ? null : (int)response.Status;
        var failure = RegisterBannerMapper.Map(code, error ?? response?.ReasonPhrase);
        _banner = failure.Banner;
        _status = "Registration failed";
        _tones.Stop();
        Publish();
    }

    private void OnIncomingCall(SIPUserAgent ua, SIPRequest request)
    {
        if (ua.IsCallActive || ua.IsCalling || ua.IsRinging || _pendingUas is not null || _uaConsult?.IsCallActive == true)
        {
            try
            {
                var busy = ua.AcceptCall(request);
                busy.Reject(SIPResponseStatusCodesEnum.BusyHere, "Busy");
            }
            catch
            {
                // Ignore.
            }

            return;
        }

        try
        {
            _pendingUas = ua.AcceptCall(request);
            try
            {
                _pendingUas.Progress(SIPResponseStatusCodesEnum.Ringing, null, null, null, null);
            }
            catch
            {
                // 180 is best-effort.
            }

            var from = request.Header.From;
            var fromText = from is null
                ? null
                : string.IsNullOrWhiteSpace(from.FromName)
                    ? from.FromURI?.ToString()
                    : $"\"{from.FromName}\" <{from.FromURI}>";
            _incomingFrom = SipAddressing.DescribeIncoming(fromText, from?.FromURI?.ToString());
            _status = $"Incoming from {_incomingFrom}";
            _tones.Play(ToneKind.Ringtone);
            IncomingStarted?.Invoke();
            Publish();
        }
        catch (Exception ex)
        {
            _status = $"Incoming call error: {ex.Message}";
            Publish();
        }
    }

    private async Task PlaceConsultCallAsync(string number)
    {
        EnsureTransport();
        _uaConsult?.Close();
        _uaConsult = new SIPUserAgent(_transport, null, isTransportExclusive: false);
        _uaConsult.ClientCallAnswered += (_, resp) =>
        {
            if (resp.Status == SIPResponseStatusCodesEnum.Ok)
            {
                _status = "Consult connected — press Ctrl+Shift+T to complete transfer";
                _tones.Play(ProgressToneMapper.FromAnswer());
                Publish();
            }
        };
        _uaConsult.ClientCallFailed += (_, err, resp) =>
        {
            _status = resp is null ? $"Consult failed: {err}" : $"Consult failed ({(int)resp.Status})";
            Publish();
        };
        _uaConsult.OnCallHungup += _ => HandleRemoteHangup(consult: true);
        _uaConsult.ClientCallRinging += (_, _) =>
        {
            _tones.Play(ToneKind.Ringback);
            _status = $"Consult ringing {number}";
            Publish();
        };

        var dst = SipAddressing.BuildInviteUri(number, _settings);
        var password = _store.GetPassword(_settings);
        var consultAudio = new WindowsAudioEndPoint(
            new AudioEncoder(),
            audioOutDeviceIndex: _settings.OutputDeviceIndex,
            audioInDeviceIndex: _settings.InputDeviceIndex);
        consultAudio.RestrictFormats(IsSupportedCodec);
        var media = new VoIPMediaSession(consultAudio.ToMediaEndPoints()) { AcceptRtpFromAny = true };
        _status = $"Consult calling {number}…";
        Publish();
        await _uaConsult.InitiateCallAsync(
            new SIPCallDescriptor(
                _settings.Username,
                password,
                dst,
                SipAddressing.BuildFromHeader(_settings),
                dst,
                null,
                null,
                _settings.EffectiveAuthUsername,
                SIPCallDirection.Out,
                null,
                null,
                null),
            media);
    }

    private VoIPMediaSession CreateMediaSession()
    {
        CleanupMedia();
        _audio = new WindowsAudioEndPoint(
            _encoder,
            audioOutDeviceIndex: _settings.OutputDeviceIndex,
            audioInDeviceIndex: _settings.InputDeviceIndex);
        _audio.RestrictFormats(IsSupportedCodec);
        _audio.OnAudioSourceEncodedSample += OnLocalEncoded;
        _session = new VoIPMediaSession(_audio.ToMediaEndPoints()) { AcceptRtpFromAny = true };
        _haveNegotiatedFormat = false;
        _session.OnAudioFormatsNegotiated += formats =>
        {
            if (formats is { Count: > 0 })
            {
                _negotiatedFormat = formats[0];
                _haveNegotiatedFormat = true;
            }
        };
        _session.OnRtpPacketReceived += OnRemoteRtp;
        return _session;
    }

    private static bool IsSupportedCodec(AudioFormat format) =>
        format.Codec is AudioCodecsEnum.PCMU or AudioCodecsEnum.PCMA or AudioCodecsEnum.G722;

    private void OnLocalEncoded(uint durationRtpTimestamp, byte[] sample)
    {
        var rec = _recorder;
        if (rec is null || sample is null || sample.Length == 0)
        {
            return;
        }

        try
        {
            var format = _haveNegotiatedFormat
                ? _negotiatedFormat
                : new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU);
            var pcm = _encoder.DecodeAudio(sample, format);
            if (pcm is { Length: > 0 })
            {
                int hz = format.Codec == AudioCodecsEnum.G722 ? 16000 : 8000;
                rec.AddLocal(pcm, hz);
            }
        }
        catch
        {
            // Drop un-decodable local frames rather than tearing down the call.
        }
    }

    private void OnRemoteRtp(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, SIPSorcery.Net.RTPPacket rtpPacket)
    {
        var rec = _recorder;
        if (rec is null || mediaType != SDPMediaTypesEnum.audio || rtpPacket?.Payload is null)
        {
            return;
        }

        try
        {
            var format = _haveNegotiatedFormat
                ? _negotiatedFormat
                : rtpPacket.Header.PayloadType switch
                {
                    8 => new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA),
                    9 => new AudioFormat(SDPWellKnownMediaFormatsEnum.G722),
                    _ => new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU)
                };

            var pcm = _encoder.DecodeAudio(rtpPacket.Payload, format);
            if (pcm is { Length: > 0 })
            {
                int hz = format.Codec == AudioCodecsEnum.G722 ? 16000 : 8000;
                rec.AddRemote(pcm, hz);
            }
        }
        catch
        {
            // Drop un-decodable frames rather than tearing down the call.
        }
    }

    private void HandleRemoteHangup(bool consult)
    {
        if (consult)
        {
            _uaConsult = null;
            if (_attendedPending)
            {
                _status = "Consult call ended";
            }

            Publish();
            return;
        }

        IncomingCleared?.Invoke();
        _pendingUas = null;
        _incomingFrom = null;
        StopRecording();
        CleanupMedia();
        _held = false;
        _muted = false;
        _attendedPending = false;
        _remoteParty = null;
        if (!_expectLocalHangup)
        {
            _tones.Play(ProgressToneMapper.FromHangup());
        }

        _status = _registered ? "Registered" : "Idle";
        _ = Task.Delay(500).ContinueWith(__ =>
        {
            MaybeDialTone();
            Publish();
        });
        Publish();
    }

    private void MaybeDialTone()
    {
        if (_disposed || !_registered)
        {
            return;
        }

        if (_pendingUas is not null || _ua?.IsCallActive == true || _ua?.IsCalling == true || _ua?.IsRinging == true)
        {
            return;
        }

        if (_digits.Length > 0)
        {
            return;
        }

        _tones.Play(ToneKind.Dial);
    }

    private void StopRecording()
    {
        try
        {
            _recorder?.Dispose();
        }
        catch
        {
            // Ignore flush errors.
        }

        _recorder = null;
    }

    private void CleanupMedia()
    {
        try
        {
            if (_audio is not null)
            {
                _audio.OnAudioSourceEncodedSample -= OnLocalEncoded;
            }

            if (_session is not null)
            {
                _session.OnRtpPacketReceived -= OnRemoteRtp;
                _session.Close("end");
            }
        }
        catch
        {
            // Ignore.
        }

        try
        {
            _audio?.CloseAudio();
        }
        catch
        {
            // Ignore.
        }

        _session = null;
        _audio = null;
        _muted = false;
    }

    private async Task TeardownSipAsync(bool unregister)
    {
        StopRecording();
        CleanupMedia();
        try
        {
            _ua?.Hangup();
            _uaConsult?.Hangup();
        }
        catch
        {
            // Ignore.
        }

        if (_reg is not null)
        {
            try
            {
                if (unregister)
                {
                    _reg.Stop();
                    await Task.Delay(400);
                }
            }
            catch
            {
                // Ignore.
            }

            _reg = null;
        }

        try
        {
            _ua?.Close();
            _uaConsult?.Close();
        }
        catch
        {
            // Ignore.
        }

        _ua = null;
        _uaConsult = null;
        _pendingUas = null;

        if (_transport is not null)
        {
            try
            {
                _transport.SIPTransportRequestReceived -= OnTransportRequest;
                _transport.Shutdown();
            }
            catch
            {
                // Ignore.
            }

            _transport = null;
        }

        _registered = false;
    }

    private void Publish()
    {
        try
        {
            StateChanged?.Invoke(Snapshot());
        }
        catch
        {
            // UI may be tearing down.
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
