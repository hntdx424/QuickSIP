# QuickSIP

Lightweight Windows SIP softphone built on .NET 10, WPF, [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) 10.x, SIPSorceryMedia.Windows, and NAudio.

QuickSIP registers with a SIP server, places and receives audio calls (G.711 µ-law/A-law and G.722), plays North-American call-progress tones, sends RFC 2833 DTMF, and records calls as stereo 8 kHz WAV files.

## Requirements

- Windows 10 version 1809 (build 17763) or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A SIP account (Asterisk, FreeSWITCH, 3CX, Kamailio, etc.)
- A microphone and speakers (or headset)

## Build

```bash
dotnet restore QuickSIP.sln
dotnet build QuickSIP.sln -c Release
```

The WPF project targets `net10.0-windows10.0.17763.0`. On Linux or macOS the same solution still restores and compiles with `EnableWindowsTargeting` (already set in `Directory.Build.props`):

```bash
dotnet build QuickSIP.sln -c Release
dotnet test QuickSIP.sln -c Release
```

The Windows UI and NAudio devices do not run on non-Windows hosts; unit tests cover tone, SIP, DTMF, settings, and recording helpers and are fully portable.

## Run

```bash
dotnet run --project src/QuickSIP/QuickSIP.csproj -c Release
```

Or start `src/QuickSIP/bin/Release/net10.0-windows10.0.17763.0/QuickSIP.exe` after a Release build.

## Usage

1. Click **Settings** (gear + label, bottom-right).
2. Enter SIP server, port, transport (UDP or TCP), username, password, and optional domain / auth username / display name.
3. Choose microphone and speaker. Set a recordings folder if you do not want the default.
4. Save, then click **Register** (or rely on “Register when QuickSIP starts”).
5. Dial a number or `sip:` URI and press **Dial** / Enter. Answer inbound calls with **Answer** or `Ctrl+Shift+A`.

Settings live in `%AppData%\QuickSIP\settings.json`. The SIP password is encrypted with Windows DPAPI (`DataProtectionScope.CurrentUser`) and is never written in plaintext.

### Call controls

| Action | UI | Notes |
| --- | --- | --- |
| Dial | Dial / Enter | Uses the number field (`sip:` URIs allowed) |
| Hang up / reject | Hang up / Escape | Rejects a ringing inbound call with 486 |
| Answer | Answer | Shown on the inbound banner |
| Hold / resume | Hold | SIP re-INVITE via SIPSorcery `PutOnHold` / `TakeOffHold` |
| Mute / unmute | Mute | Pauses the local microphone |
| Blind transfer | Blind transfer | SIP `REFER` to the number in the display |
| Attended transfer | Attended transfer | Hold, dial consult, then complete with the same command |
| Record | Record | Stereo 8 kHz WAV; toggle to stop |
| Register | Register | `Ctrl+Shift+R` also re-registers |

Inbound calls while registered play a local ringtone, flash the taskbar, and show Answer / Reject. `OPTIONS` qualify pings are answered with **200 OK**.

## Shortcuts

All letter shortcuts use **Ctrl+Shift**. They can be disabled in Settings; Escape and Enter always work.

| Shortcut | Action |
| --- | --- |
| `Ctrl+Shift+D` | Dial |
| `Ctrl+Shift+A` | Answer |
| `Ctrl+Shift+H` | Hang up / reject |
| `Ctrl+Shift+O` | Hold / resume |
| `Ctrl+Shift+M` | Mute / unmute |
| `Ctrl+Shift+B` | Blind transfer (SIP REFER) |
| `Ctrl+Shift+T` | Attended transfer start / complete |
| `Ctrl+Shift+C` | Start / stop recording |
| `Ctrl+Shift+R` | Register |
| `Escape` | Hang up / reject |
| `Enter` | Dial |
| Numpad `*` | DTMF `*` |
| Numpad `/` | DTMF `#` |

In-call digits (including the numpad) are sent as **RFC 2833 / RFC 4733** telephone-events.

## Tones

Successful **REGISTER** starts a dial tone (until the first digit). Failed **REGISTER** is silent; a banner explains the SIP error instead.

| Event | Tone | Frequencies / cadence |
| --- | --- | --- |
| Registered, idle | Dial | 350 + 440 Hz, continuous |
| Outbound 180/183 (no early media) | Ringback | 440 + 480 Hz, 2 s on / 4 s off |
| Inbound ringing | Ringtone | 440 + 480 Hz, 1 s on / 3 s off |
| 486 / 600 | Busy | 480 + 620 Hz, 0.5 s on / 0.5 s off |
| 480 / 408 / 503 / 603 | Congestion | 480 + 620 Hz, 0.25 s on / 0.25 s off |
| 200 OK | Answer | 425 Hz, ~180 ms |
| Local or remote BYE | Hangup | 480 + 440 Hz, ~400 ms |
| Other INVITE failures | Error | 480 + 620 Hz, short burst |
| 183 with SDP | _(none)_ | Early media from the remote party is played instead |

## Registration banners

Failed REGISTER shows a red banner at the bottom of the window:

| Condition | Banner |
| --- | --- |
| No SIP response / DNS / connect / timeout wording | Cannot reach the SIP server… |
| `401` / `403` / `407` | Authentication rejected |
| `404` | Account not found |
| `408` / `504` | SIP server timed out |
| Other `5xx` | SIP server error |

## Recording

- Format: 16-bit PCM WAV, **stereo**, **8 kHz**
- Left channel: local microphone
- Right channel: remote party
- Default folder: `%UserProfile%\Documents\QuickSIP\Recordings`
- Override the folder in Settings. Files are named `quicksip-yyyyMMdd-HHmmss.wav`.

## Project layout

```
QuickSIP.sln
src/QuickSIP/            WPF app (SIP, audio, UI)
src/QuickSIP.Core/       Settings, DPAPI, tones, DTMF, banners, WAV writer
tests/QuickSIP.Tests/    xUnit tests for the helpers above
```

## Limitations

- Windows-only runtime (WPF + NAudio + SIPSorceryMedia.Windows). Linux CI can build and test helpers only.
- Audio codecs offered are G.711 PCMU, G.711 PCMA, and G.722. Opus/G.729 are not enabled.
- One active call at a time (plus one consult leg during attended transfer). A second inbound INVITE is rejected with 486.
- No video, BLF, presence, or conference mixer.
- NAT traversal is whatever your registrar provides via Contact/Record-Route. There is no built-in STUN/TURN wizard.
- DTMF is RFC 2833 telephone-event only (not SIP INFO).
- DPAPI passwords cannot be moved to another Windows user or machine.
- Attended transfer depends on the far end accepting `REFER` with `Replaces`.
- Call recording taps decoded PCM; encrypted / unknown payloads are skipped.

## License

Source in this repository is provided for use with your own SIP accounts. SIPSorcery is BSD 3-Clause; NAudio is MIT. Follow those licenses when redistributing.
