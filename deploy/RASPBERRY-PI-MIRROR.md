# ADA on a Raspberry Pi smart mirror

The Pi runs **`ADA-MKII-Web` plus Chromium in kiosk mode**. It does *not* run the
server: SQL Server has no ARM64 Linux build, so `ADA-MKII-Server` stays on an
x86-64 VPS and the Pi talks to it over HTTPS like any other client.

```
   Raspberry Pi (arm64)                        VPS (x86-64)
   ┌──────────────────────────┐                ┌──────────────────┐
   │ Chromium ──► ADA-MKII-Web│ ── HTTPS ────► │ ADA-MKII-Server  │
   │  (kiosk)     (localhost) │   + bearer     │      │           │
   │                │         │     token      │  SQL Server      │
   │   mic ──► ALSA ┤         │                └──────────────────┘
   │   spk ◄── spd  ┘         │
   └──────────────────────────┘
```

Hosting the web head **on the Pi** rather than on the VPS is deliberate.
`ADA-MKII-Web` is Blazor *Server*: the UI event loop runs wherever the head runs,
and the chat composer binds on `oninput`, so every keystroke is a round trip. Over
`localhost` that is free; to a VPS on another continent it is not. It also makes
`localhost` the origin, which browsers treat as a secure context.

## Hardware

| | |
|---|---|
| Board | **Pi 5, 8 GB** recommended. Pi 4 / 4 GB is fine for display only. |
| OS | **64-bit Raspberry Pi OS.** Mandatory - .NET 10 and the Whisper `linux-arm64` binaries both need `arm64`. |
| Microphone | USB mic or a ReSpeaker array. Mirror mics are far-field and Whisper degrades badly on a poor signal. |
| Storage | 32 GB+. The Whisper model is ~75 MB (Tiny) to ~140 MB (Base). |

## 1. Provision a device token

The mirror has no keyboard, so it cannot sign in. Mint a token once, from
anywhere, and give it to the Pi:

```bash
curl -s -X POST https://your-ada-domain/api/auth/login -H 'Content-Type: application/json' -d '{"username":"you","password":"your-password"}'
```

The `token` in the response is a long-lived device credential. It is revocable
server-side and grants exactly one account's access - but anyone who can reach
the mirror's page **is** that account, with no login. Only do this on a device you
physically control.

## 2. Publish for the Pi

Build on your workstation, not the Pi:

```bash
dotnet publish ADA-MKII-Web -c Release -r linux-arm64 --self-contained -o out/mirror
```

Copy `out/mirror` to `/opt/ada/web` on the Pi.

## 3. Configure

Put this in `/etc/ada/mirror.env`, `chmod 0600`:

```bash
Ada__Client__BaseAddress=https://your-ada-domain
Ada__Kiosk__DeviceToken=<the token from step 1>

# Speech on the Pi's own hardware rather than through the browser. Chromium on
# Raspberry Pi OS cannot do speech recognition at all - distribution builds ship
# without the credentials Google's speech service needs.
Ada__Speech__OnDevice=true
Ada__Speech__AlsaDevice=default
Ada__Speech__ModelDirectory=/var/lib/ada/speech
Ada__Speech__Model=Tiny

# Wake word. Off by default; every other head keeps push-to-talk.
Ada__Speech__WakeWord__Enabled=true
Ada__Speech__WakeWord__Phrase=hey ada
Ada__Speech__WakeWord__MinimumLevel=0.015
```

`arecord -l` lists capture devices if `default` is not the microphone you want; a
USB mic is usually `plughw:1,0`.

## 4. Packages and permissions

```bash
sudo apt install -y alsa-utils speech-dispatcher chromium-browser
sudo usermod -aG audio ada
sudo mkdir -p /var/lib/ada/speech && sudo chown ada:ada /var/lib/ada/speech
```

`alsa-utils` provides `arecord` and `speech-dispatcher` provides `spd-say`; the
head checks for both and reports itself unsupported rather than failing at first
use. The `audio` group is what grants access to the ALSA device - there is no
runtime permission prompt on Linux.

## 5. Kiosk

Point Chromium at the mirror route on the local head:

```bash
chromium-browser --kiosk --noerrdialogs --disable-infobars --incognito http://localhost:5200/mirror
```

`/mirror` uses its own chrome-free layout - no navigation, no account name, no
sign-out. `/` still serves the ordinary dashboard if you want it on another screen.

Hide the cursor with `unclutter`, and disable blanking:

```bash
xset s off -dpms
```

## Tuning the wake word

`MinimumLevel` is the setting that matters. It is an RMS threshold below which a
slice of audio is discarded without being transcribed, and it is what keeps the
Pi from running Whisper against an empty room around the clock. Too high and the
mirror ignores you; too low and it burns CPU and Whisper starts inventing text
from room noise.

Raise it if the mirror wakes at nothing. Lower it if it ignores you from across
the room.

A longer phrase triggers far more reliably than a short one - "hey ada" beats
"ada" by a wide margin, because Whisper has more to match against.

## Known limits

- **Slice boundaries.** The detector transcribes fixed windows, so a wake phrase
  spoken exactly across a boundary is missed. Say it again.
- **CPU.** Transcribing every non-silent slice is the honest, dependency-free
  approach, not the efficient one. A purpose-built spotter (Porcupine,
  openWakeWord) would cost a fraction; `IWakeWordDetector` is the seam to
  replace when that matters.
- **Voice quality.** `speech-dispatcher`'s default espeak-ng voice is
  intelligible and robotic. ElevenLabs is the good-sounding alternative, and is
  paid and opt-in.
