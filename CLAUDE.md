# ADA — Advanced Digital Assistant

A personal voice-and-text assistant targeting **Windows, Android and Linux**, plus a lower-priority Discord text bot. .NET 10 throughout.

## Read this first

**This is a ground-up rebuild.** Every file currently in the repo outside `ADA-MKII-UI/Resources/` is placeholder scaffolding to be replaced. **Git history is not a reference** — do not mine earlier commits for code or design intent. This document is the sole source of architectural truth. If the code disagrees with it, the code is what changes.

**The naming trap — internalise this before touching anything:**

| Project | Direction | Meaning |
|---|---|---|
| `ADA-MKII-API` | **Outbound** | A class library of *clients we call*: OpenAI, ElevenLabs, weather. Not a web server. |
| `ADA-MKII-Server` | **Inbound** | The ASP.NET Core HTTP API *we host* on the VPS. This is the web server. |

**Naming convention (deliberate, do not "fix"):** folders and projects use `ADA-MKII-*`; root namespaces use `ADA_MKII_*` with dotted sub-namespaces (`ADA_MKII_Core.Abstractions`). Hyphens are illegal in namespaces, so the two differ by design. New projects follow the same pattern.

## Settled decisions

These are closed. Do not reopen them without an explicit instruction.

| Decision | Choice | Why |
|---|---|---|
| **Linux strategy** | Blazor Hybrid + web head | MAUI has no official Linux desktop target. Razor components are authored once and hosted in a `BlazorWebView` on Windows/Android and in a Blazor Server head for Linux browsers. |
| **Data topology** | All access via a self-hosted web API | Only the server holds SQL credentials and the EF `DbContext`. Nothing else is safe to ship to a phone. |
| **Project count** | 8 | The original 5 plus `Server`, `Web`, `UI-Shared`. DTOs and the HTTP client SDK fold into Core rather than getting their own projects. |
| **MAUI targets** | `net10.0-android` + `net10.0-windows10.0.19041.0` only | iOS and MacCatalyst are dropped — not required, and they cost build time and a Mac. |
| **Existing sample UI** | Strip it | The MAUI project is currently the stock project/task-manager sample. Delete its content; keep `Resources/` (icons, fonts, splash). |
| **Network + auth** | Public HTTPS + per-device bearer token | Reverse proxy with Let's Encrypt, long random token per device, rate limiting. |

## Project graph

| Project | SDK | TFM(s) | Purpose |
|---|---|---|---|
| `ADA-MKII-Core` | `Microsoft.NET.Sdk` | `net10.0` | Domain model, **all abstractions**, DTOs, `ApiRoutes`, orchestration pipeline, and the typed HTTP client SDK. |
| `ADA-MKII-API` | `Microsoft.NET.Sdk` | `net10.0` | Outbound providers: OpenAI, ElevenLabs, weather. **Server-only.** |
| `ADA-MKII-Data` | `Microsoft.NET.Sdk` | `net10.0` | EF Core + SQL Server, `AdaDbContext`, entities, migrations. **Server-side only.** |
| `ADA-MKII-DataManager` | `Microsoft.NET.Sdk` + `UseWPF` | `net10.0-windows` | Operator tool. Creates and administers accounts against the database directly. **The only way to create an account.** |
| `ADA-MKII-Server` | `Microsoft.NET.Sdk.Web` | `net10.0` | Minimal API hosted on the VPS. The only process holding secrets. |
| `ADA-MKII-UI-Shared` | `Microsoft.NET.Sdk.Razor` | `net10.0` | Razor Class Library — every component, page and CSS. Shared by both UI heads. |
| `ADA-MKII-UI` | `Microsoft.NET.Sdk.Razor` + `UseMaui` | `net10.0-android;net10.0-windows10.0.19041.0` | Blazor Hybrid host + device STT/TTS/SecureStorage. |
| `ADA-MKII-Web` | `Microsoft.NET.Sdk.Web` | `net10.0` | Blazor Server head for Linux. Web Speech API interop. |
| `ADA-MKII-Discord` | `Microsoft.NET.Sdk` (Worker) | `net10.0` | Discord.Net bot, text only. **Lowest priority.** |

### Reference edges

```
Core          → (no project references)
API           → Core
Data          → Core
Server        → Core, API, Data
UI-Shared     → Core
UI (MAUI)     → Core, UI-Shared
Web           → Core, UI-Shared
Discord       → Core
```

```
                        ┌──────────────────┐
                        │   ADA-MKII-Core  │  abstractions · DTOs · pipeline · HTTP client
                        └─▲──▲──▲──▲───▲───┘
            ┌─────────────┘  │  │  │   └──────────────┐
            │                │  │  └────────┐         │
   ┌────────┴───────┐ ┌──────┴──┴─────┐ ┌───┴──────┐ ┌┴─────────────┐
   │  ADA-MKII-API  │ │ ADA-MKII-Data │ │UI-Shared │ │   Discord    │
   │ OpenAI/11Labs  │ │ EF + SqlServer│ │  (RCL)   │ │ (text only)  │
   └────────▲───────┘ └───────▲───────┘ └─▲──────▲─┘ └──────┬───────┘
            │                 │           │      │          │
            └────────┬────────┘     ┌─────┘      └────┐     │
                     │              │                 │     │
            ┌────────┴────────┐ ┌───┴──────┐ ┌────────┴─────┴──┐
            │ ADA-MKII-Server │ │ UI (MAUI)│ │  ADA-MKII-Web   │
            │  (ASP.NET Core) │ │  Hybrid  │ │  Blazor Server  │
            └────────▲────────┘ └────┬─────┘ └────────┬────────┘
                     │               │                │
                     └───── HTTPS + bearer token ─────┘
```

**Mechanical check:** `API` and `Data` have exactly one consumer — `Server`. Therefore no head transitively pulls `Microsoft.EntityFrameworkCore.*`, `Microsoft.Data.SqlClient`, `OpenAI` or `ElevenLabs-DotNet`. Verify with:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/ada-dev/scripts/check-layering.ps1
```

It exits non-zero on a violation, so it doubles as a CI gate. An EF Core or provider-SDK hit in `UI`, `Web`, `Discord` or `UI-Shared` is a layering violation, not a curiosity.

> **Operational commands** — migrations, running the heads, smoke-testing the API, and the causes of this repo's recurring build failures — are documented in the `ada-dev` skill at `.claude/skills/ada-dev/SKILL.md`. This file stays the source of truth for architecture; that one for procedure.

Core carries the HTTP client SDK (`HttpConversationStore`, `HttpSettingsStore`) as a consequence of the 8-project choice. Core therefore takes `Microsoft.Extensions.Http` and `Microsoft.Extensions.Http.Resilience` — but still no EF, no MAUI, no ASP.NET, no provider SDKs.

## Runtime data flow

The single most load-bearing property of this design:

**All database access and all third-party API traffic goes through `ADA-MKII-Server`. No client does either directly.**

```
MAUI (Win/Android) ─┐
Web head (Linux)   ─┼─ HTTPS + bearer token ─→ ADA-MKII-Server ─┬─→ SQL Server (localhost only)
Discord bot        ─┘                                           └─→ OpenAI / ElevenLabs / weather
```

- No client opens a SQL connection. SQL Server binds to **localhost on the VPS** and is unreachable from any device.
- No client holds a provider API key. A client posts to `/api/chat`; the server performs the LLM call and any tool invocations.
- **Each client carries exactly one secret: its own device bearer token.** A compromised device leaks a revocable token — not the SQL credentials, not the OpenAI key. This is the entire rationale for the topology.

**One deliberate exception — on-device speech.** STT and TTS run locally wherever the platform supports it: MAUI via `SpeechToText`/`TextToSpeech`, the web head via the browser's Web Speech API. That audio never reaches the server. Only the fallback paths (Firefox, or ElevenLabs voice output) round-trip through `/api/speech/*`. Streaming mic audio to the VPS for something the device does free would be worse on both latency and cost.

## Layering rules

Self-audit against these before finishing any change.

1. **Core references no project.** Its only packages are `Microsoft.Extensions.{DependencyInjection,Logging,Options}.Abstractions` and `Microsoft.Extensions.Http[.Resilience]`.
2. **Core defines every cross-project interface.** Implementations live in the project that owns the technology.
3. **`Data` and `API` may be referenced only by `Server`** — plus `DataManager`, which references `Data`. That is the single deliberate exception: `DataManager` is a second server-side process, not a head, and it exists precisely to do what no client may. It therefore holds SQL credentials and **must only run somewhere trusted** — on the VPS, or over a VPN or SSH tunnel. Never install it on a shared or mobile device.
4. **No head (`UI`, `Web`, `Discord`) may reference `Data`, `API`, or `Server`.**
5. **`UI-Shared` contains no platform-conditional code** (`#if ANDROID` / `#if WINDOWS`) and **must not reference `Microsoft.Maui.*`**. If it does, the Web head stops compiling — that is the enforcement mechanism, and it is a feature.
6. **Only `Server` reads provider secrets.** Heads hold exactly one secret: the device bearer token.
7. **EF entities are internal to `Data`** and never cross the wire. Stores return Core DTOs.
8. **Implementations are named `<Provider><Interface>`:** `OpenAiLlmProvider`, `SqlConversationStore`, `HttpConversationStore`, `MauiSpeechToTextService`, `WebSpeechToTextService`.

## Key abstractions

All interfaces live in `ADA_MKII_Core.Abstractions`.

| Interface | Shape | Implementations |
|---|---|---|
| `ILlmProvider` | `IAsyncEnumerable<LlmDelta> StreamAsync(LlmRequest, CancellationToken)` | `OpenAiLlmProvider` and `EchoLlmProvider`, chosen per turn by `RoutingLlmProvider` (API) |
| `IAssistantTool` | `Name`, `Description`, `JsonSchema`, `Task<ToolResult> InvokeAsync(JsonElement, CancellationToken)` | `CreateNoteTool`, `SearchNotesTool`, `CreateEventTool`, `ListEventsTool`, `RememberTool`, `RecallTool` (**Core**, see below); `WeatherTool` will be API |
| `IToolRegistry` | Discovery + dispatch of `IAssistantTool` | Core |
| `IIntentDispatcher` | `Task<CommandResult?> TryHandleAsync(...)` — deterministic commands before the LLM | Core |
| `IAssistantPipeline` | `IAsyncEnumerable<ChatStreamEvent> RunAsync(ChatRequest, CancellationToken)` — the one entry point | `AssistantPipeline` (Core, server-side) **and** `HttpAssistantPipeline` (Core, client-side over SSE) |
| `IConversationStore` | `Create` / `Get` / `List` / `AppendMessage` | `SqlConversationStore` (Data) **and** `HttpConversationStore` (Core) |
| `ISettingsStore` | `GetAsync<T>(key, ct)` / `SetAsync(key, value, ct)` | `SqlSettingsStore` (Data), `HttpSettingsStore` (Core) |
| `ISecretStore` | Device token only | `SecureStorageSecretStore` (MAUI), config-backed (Web), env-backed (Discord) |
| `ISpeechToTextService` | `RequestPermissionAsync`, `IAsyncEnumerable<SpeechPartial> ListenAsync(CultureInfo, ct)` | `MauiSpeechToTextService`, `WebSpeechToTextService`, `NullSpeechToTextService` |
| `ITextToSpeechService` | `SpeakAsync(text, options, ct)`, `StopAsync()` | `MauiTextToSpeechService`, `WebSpeechSynthesisService`, `ElevenLabsTtsService` (API) |
| `INoteStore` | `Create` / `Get` / `List` / `Search` / `Update` / `Delete` | `SqlNoteStore` (Data), `HttpNoteStore` (Core) |
| `ICalendarStore` | Series CRUD plus `ListOccurrencesAsync(from, to, ct)` and `CancelOccurrenceAsync` | `SqlCalendarStore` (Data), `HttpCalendarStore` (Core) |
| `IMemoryStore` | `Create` / `ListRecent` / `Search` / `Delete` | `SqlMemoryStore` (Data), `HttpMemoryStore` (Core) |
| `IWeatherProvider` | Typed integration seam | API |

**Critical design note:** `IConversationStore`, `ISettingsStore` **and `IAssistantPipeline`** each have a server-side implementation and an HTTP client-side one. That symmetry is what lets `UI-Shared` be byte-identical across every head: the UI depends only on the abstraction and never learns whether orchestration happens in-process or across the network.

**Conventions:**
- Every async abstraction method takes `CancellationToken` as the **last parameter, with no default value** — this forces callers to think about it.
- Use the in-box `TimeProvider`. Do not invent an `IClock`.

## Configuration and secrets

**Do not store API keys in the database.** The placeholder `APIKey` entity in `ADA-MKII-Data/Model.cs` is to be deleted. It makes the database the trust root (keys land in plaintext backups), and it is circular — you need the connection string before you can read the key store, so the most sensitive secret can never live there anyway.

| Environment | Mechanism |
|---|---|
| Dev | `dotnet user-secrets` on `ADA-MKII-Server` |
| Prod | Environment variables from a `0600` systemd `EnvironmentFile` (e.g. `Ada__OpenAI__ApiKey`) |
| Non-secret defaults | `appsettings.json` / `appsettings.Production.json`, committed |

**Keep the `Setting` table**, repurposed for non-secret user preferences: model name, system prompt/persona, voice id, `Voice:UseElevenLabs`, temperature, monthly spend cap. That is what a database is genuinely good for here, and clients can edit it through the API.

Bind config with the options pattern and `.ValidateDataAnnotations().ValidateOnStart()` so a missing key fails at boot, not at first request.

### Choosing a model backend

`OpenAiLlmProvider` speaks the OpenAI wire format, which Ollama, Groq and OpenRouter also speak — so leaving OpenAI is a URL change, not a code change. `Ada:OpenAI:BaseUrl` sets the server default; a user can override it per account from the settings page (`llm.baseUrl`, with `llm.model`).

**The API key only ever goes to the endpoint the operator configured.** Point ADA elsewhere and it connects unauthenticated, which is what a local model wants anyway. Without that rule, any signed-in user could name a host and be handed this server's OpenAI key. Set `Ada:OpenAI:AllowUserBaseUrl=false` to remove the override entirely when accounts belong to other people.

`RoutingLlmProvider` picks the backend per turn rather than at startup, so a server deployed with no key at all still works the moment someone points it at their own endpoint. With neither configured nor chosen, `EchoLlmProvider` answers — which keeps a fresh deployment testable before any credential exists.

### Finding the server

`IServerAddressProvider` answers "where is ADA-MKII-Server". It cannot be a normal setting: you need the address before you can ask the server anything, so it is device-local by necessity.

| Head | Source | Editable |
|---|---|---|
| Web | `Ada:Client:BaseAddress` in config | No — deployed next to a known server |
| MAUI | `Preferences`, with a field on the login screen | **Yes** — the app ships not knowing which server it will talk to |

The MAUI default is `10.0.2.2:5100` on Android (the emulator's alias for the host loopback) and `localhost:5100` on Windows. Those are starting points only: a physical phone must be pointed at a LAN address, a tunnel, or a domain, which is exactly why the field exists. The address is read each time a typed client is constructed, so a change takes effect without restarting the app.

A hostname is not a secret, so it lives in `Preferences`, not `SecureStorage`. The bearer token is, and it does not.

### Accounts and login

Users sign in with a username and password. **Accounts are created only in `ADA-MKII-DataManager`** — the API has no registration endpoint, so there is no account-creation surface to attack no matter what a caller sends.

The flow:

1. An operator creates an account in DataManager. The password is hashed with PBKDF2-HMAC-SHA256 (`PasswordHasher` in Core); the hash format carries its own iteration count, so the cost can be raised later without invalidating existing passwords.
2. A client posts credentials to `/api/auth/login`. On success the server mints a bearer token, stores only its SHA-256 hash, and returns the token **once**.
3. Every later request carries that token. The auth handler resolves it to an account id, which becomes the principal's `NameIdentifier`.
4. `IAccountContext` reads that id, and the SQL stores scope every query by it.

Login verifies a password even when the username is unknown, against `PasswordHasher.DummyHash`. Skipping the hash for missing users would make response time a username oracle. `/api/auth/login` also carries a tighter rate limit than the rest of the API, because it is the one endpoint worth brute-forcing.

**Disabling an account immediately invalidates its tokens** — the check is part of the token lookup query, not a second step. Changing a password revokes them too.

**Where each head keeps its token:** MAUI → `SecureStorage`, surviving restarts; Web → server memory for the life of the Blazor circuit, never rendered into the browser (so a page reload means signing in again — deliberate, versus putting a bearer token in `localStorage`); Discord → environment variable.

Local development connects to `(localdb)\MSSQLLocalDB`, configured in `appsettings.Development.json`. That connection string uses trusted auth and contains no secret, which is why it is safe to commit; the production one never is.

## DI composition roots

One `AddAda*` extension per project:

| Extension | Lives in | Registers |
|---|---|---|
| `AddAdaCore()` | Core | Pipeline, tool registry, intent dispatcher |
| `AddAdaClient(baseAddress, tokenProvider)` | Core | Typed `HttpClient` + `Http*` stores |
| `AddAdaProviders(IConfiguration)` | API | Typed HttpClients + resilience handlers |
| `AddAdaData(connectionString)` | Data | `AddDbContextPool` + `Sql*` stores |
| `AddAdaSharedUi()` | UI-Shared | UI state services only |

Each head composes exactly:

- **Server:** `AddAdaCore()` + `AddAdaProviders()` + `AddAdaData()`
- **MAUI:** `AddAdaClient()` + `AddAdaSharedUi()` + `MauiSpeechToTextService` / `MauiTextToSpeechService` / `SecureStorageSecretStore`
- **Web:** `AddAdaClient()` + `AddAdaSharedUi()` + `WebSpeechToTextService` / `WebSpeechSynthesisService`
- **Discord:** `AddAdaClient()` + `Null*` speech services

## Cross-cutting conventions

**Logging.** `Microsoft.Extensions.Logging` abstractions everywhere; inject `ILogger<T>`, never use a static logger. Server adds Serilog → console (journald) + rolling file, with `ConversationId` in a log scope per turn. **Never log message bodies at Information level. Never log keys or tokens.**

Use **source-generated `[LoggerMessage]` methods**, not `logger.LogInformation(...)` — CA1848 enforces this at build time, and it keeps every message template in one auditable place. See `ADA-MKII-Server/Logging/ServerLog.cs` for the pattern.

**Cancellation.** Sources per head: `HttpContext.RequestAborted` (Server), component disposal (Blazor), page lifecycle plus an explicit stop button (MAUI voice). Streaming endpoints must honour client disconnect so a cancelled chat stops burning tokens.

**Error handling.**
- Server returns RFC 9457 `ProblemDetails` for every failure (`AddProblemDetails()` + exception handler middleware). No raw exception text to clients.
- The HTTP client maps non-2xx to a typed `AdaApiException(ProblemDetails, HttpStatusCode)`, and an unreachable server to `AdaUnreachableException`. **Catch the latter, not the transport exceptions.** A dead server surfaces as `HttpRequestException`, `TaskCanceledException` or Polly's `TimeoutRejectedException` depending on which layer gave up first; getting that set right in every component is a trap, and missing one turns an offline server into a 500.
- Client resilience is tuned for a person waiting, not a background service: 5 s per attempt, 12 s total, two retries. The defaults (30 s across three retries) leave a login screen silent for half a minute when the server is simply off.
- **JS interop must not run during static prerendering**, including from `DisposeAsync` — the scoped service is disposed at the end of the render, when no JS runtime exists. Teardown paths use `WebSpeechModule.Current`, which returns the module only if already imported, rather than `GetAsync`, which would import it.
- Outbound provider calls are wrapped in `Microsoft.Extensions.Http.Resilience` (retry with jitter, total timeout, circuit breaker) — this protects against provider flakiness and bounds cost during an outage.
- Never swallow exceptions. Never `catch { }`.

**Offline (mobile).** Phase 1 has **no offline cache.** The MAUI head watches `Connectivity.Current.NetworkAccess` and shows an explicit offline banner with the composer disabled. A cache without an outbox is a lie, and an outbox needs conflict rules this app does not yet have. Consequently, `Microsoft.Data.Sqlite.Core` and `SQLitePCLRaw.bundle_green` are to be **removed** from `ADA-MKII-UI.csproj`.

## Voice pipeline

**Flow:** mic → STT partials stream into the composer → final transcript → `POST /api/chat` (SSE) → assistant deltas render → completed text → `ITextToSpeechService.SpeakAsync`.

Push-to-talk only. **No wake word** in phase 1 — the battery and false-trigger cost is not yet worth it. **Barge-in:** starting a listen must call `StopAsync()` on TTS first.

`UI-Shared` sees only the two interfaces plus a `VoiceSessionState` enum (`Idle | Listening | Thinking | Speaking`). The head supplies the implementation.

| Head | STT | TTS |
|---|---|---|
| MAUI (Win/Android) | **Whisper.net**, on-device, via `ISpeechRecognitionEngine` ✅ | `Microsoft.Maui.Media.TextToSpeech.Default` ✅ |
| Web / Linux | JS module wrapping `webkitSpeechRecognition`, partials returned via `DotNetObjectReference` ✅ | `window.speechSynthesis` ✅ |
| Web fallback (Firefox) | `MediaRecorder` → `POST /api/speech/stt` | `/api/speech/tts` → `audio/mpeg` in an `<audio>` element |
| Discord | `NullSpeechToTextService` | `NullTextToSpeechService` |

### Why Whisper on MAUI, and the seam around it

This document originally specified `CommunityToolkit.Maui.Media.SpeechToText`. That API **no longer exists** — the toolkit removed it before its .NET 10 line, and it is absent from 13.0.0, 14.2.2 and 15.0.0 alike. Downgrading is not open either, since versions that had it target net9 only.

A survey found no drop-in replacement: the only MAUI-branded STT plugin tops out at net9 and is paid commercial software, every Xamarin-era plugin targets `MonoAndroid`/`UAP`, and `Vosk` ships no Android binaries. Of the platform APIs, Android's `SpeechRecognizer` is fine but **Windows is blocked** — `Windows.Media.SpeechRecognition` requires MSIX package identity and is documented as unavailable to unpackaged apps, which this head is by design.

**Whisper.net** was chosen because it runs on-device on both targets, needs no key, and keeps audio on the device — the principle this architecture already commits to for speech.

The seam is deliberate, because this choice may not be permanent:

```
UI  →  ISpeechToTextService            (Core; the only thing the UI sees)
       └─ EngineSpeechToTextService    (Core; wiring, identical on every head)
            ├─ IAudioCapture           (head; microphone, platform work)
            └─ ISpeechRecognitionEngine (SWAPPABLE — Whisper today, Azure later)
```

Adding Azure Speech means writing one `ISpeechRecognitionEngine` and changing one registration. Nothing above that interface knows which engine is running.

**Known costs of Whisper, and they are real:**
- **No interim results.** Whisper transcribes a finished recording, so the user sees their words once, at the end, rather than as they speak. `SupportsInterimResults` reports this honestly. Azure Speech is the streaming alternative if that matters more than staying on-device.
- **A ~140 MB model** is downloaded on first use and cached, rather than shipped in the app package.
- **No 32-bit ARM.** The native binaries cover `android-arm64-v8a`, `android-x86`, `android-x86_64` and `win-x64/arm64/x86`. `IsSupported` checks the architecture so the microphone is hidden rather than failing at first use.

**ElevenLabs is opt-in, not default** — gated on the `Voice:UseElevenLabs` setting. On-device TTS is free and lower-latency; this is a direct cost control.

### Platform permissions (neither is present today)

**Android** — `Platforms/Android/AndroidManifest.xml` currently declares only `INTERNET` and `ACCESS_NETWORK_STATE`. It needs:
- `<uses-permission android:name="android.permission.RECORD_AUDIO" />`
- a package-visibility block, without which `SpeechRecognizer` silently finds no service on API 30+:
  ```xml
  <queries>
    <intent><action android:name="android.speech.RecognitionService" /></intent>
  </queries>
  ```
- a runtime prompt via `SpeechToText.Default.RequestPermissions(ct)` before the first listen.

**Windows** — `Platforms/Windows/Package.appxmanifest` needs `<DeviceCapability Name="microphone"/>`. Nuance: the project sets `WindowsPackageType=None` (unpackaged), so appxmanifest capabilities are **not enforced**. The real dev-time gate is Windows Settings → Privacy → Microphone → "Let desktop apps access your microphone". Declare the capability anyway for a future packaged build.

### Known Windows STT gotcha

On Windows, `SpeechToText.ListenAsync` can stall after emitting its first partial result rather than completing. Handle this inside `MauiSpeechToTextService`: accumulate partials from the `IProgress<string>` callback, apply a short `CancelAfter` grace period on Windows to force completion, and treat a `TaskCanceledException` with non-empty accumulated text as a successful recognition. Keep the quirk behind a named constant with an explanatory comment. Do not let it leak into `UI-Shared`.

## Solution hygiene

| Item | Position |
|---|---|
| `Directory.Build.props` | **Done.** Hoists `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`. `ADA-MKII-UI` opts out of warnings-as-errors while the sample content remains. Do **not** add `EnforceCodeStyleInBuild` without carving the IDE codes out of `TreatWarningsAsErrors` — otherwise a stray blank line breaks the build. Style is enforced by `dotnet format` instead. |
| `Directory.Packages.props` | **Done — central package management.** Versions live there and nowhere else; csprojs carry `<PackageReference Include="..." />` with no `Version`. Version drift across projects is the top multi-project bug source. |
| `global.json` | **Done.** Pinned to `10.0.400` with `rollForward: latestFeature` — two SDKs are installed (10.0.302, 10.0.400), so pin for reproducibility. |
| `.editorconfig` | **Done.** File-scoped namespaces required, `_camelCase` private fields, sorted usings, IDE0055 as a warning. **CA1707 is suppressed** — it fires on the `ADA_MKII_*` namespaces, which are deliberate. |
| `nuget.config` | **Done.** `<clear/>` + nuget.org only, so machine-level feeds cannot surprise a different box or CI. |
| `ADA-MKII.slnx` | **Keep.** Add the three new projects to it. |
| `ApplicationId` | **Set** to `io.github.justiphi.ada`. Change it before the first Android install if a real domain becomes available — changing it later orphans installed app data. |
| MAUI project SDK | **`Microsoft.NET.Sdk.Razor`**, not the base SDK. `BlazorWebView` hooks into the static web assets targets, and the base SDK fails with `MSB4057: StaticWebAssetsPrepareForRun does not exist`. |
| Android min API | **24.0**, not the MAUI default of 21 — `AddMauiBlazorWebView()` is only supported on Android 24+. |
| Dev ports | Server `5100`/`7100`, Web `5200`/`7200`. Set deliberately so the two ASP.NET processes never collide. |
| `Syncfusion.Maui.Toolkit` | **Remove.** XAML-only, therefore dead weight under Blazor Hybrid, and it requires a registered licence key at runtime. |
| Testing | xUnit v3 + NSubstitute + Shouldly in a `tests/` folder. Deferred — not part of the initial 8-project graph. **Avoid FluentAssertions**, v8+ is commercially licensed. |

## Build-out sequence

Critical path is **0 → 1 → 2 → 3 → 4**. Voice, Android and Discord are leaves.

0. **Repo hygiene — COMPLETE.** This file plus `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, `nuget.config`; hoisted properties and package versions out of all five csprojs. The four non-MAUI projects build clean with zero warnings under warnings-as-errors. *Set the compiler policy all later code is written under.*
1. **Skeleton — COMPLETE.** Created `Server`, `Web`, `UI-Shared`; wired all eight `ProjectReference` edges; stripped the MAUI sample (78 files down to 20); retargeted TFMs; dropped Syncfusion, both SQLite packages and `CommunityToolkit.Mvvm`; converted the MAUI head to Blazor Hybrid. All eight projects build clean with **zero warnings**, and the MAUI `TreatWarningsAsErrors` opt-out has been removed. The `NU1903` advisory is gone with `SQLitePCLRaw`.
2. **Data + Server foundation — COMPLETE.** `AdaDbContext` with `Conversation`, `Message`, `Setting` and `DeviceToken`; `APIKey` dropped; EF SQLite → SqlServer; `InitialCreate` migration applied. Server has `/health` (anonymous), device-token bearer auth, ProblemDetails, Serilog, rate limiting, and `/api/conversations` + `/api/settings`. Verified end to end against SQL Server.
3. **LLM end-to-end — COMPLETE.** `AssistantPipeline` in Core, `OpenAiLlmProvider` in API, `POST /api/chat` as SSE, and the HTTP client (`HttpConversationStore`, `HttpSettingsStore`, `HttpAssistantPipeline`). Cost controls are in from the start: max output tokens, history trimming, per-message token persistence and a monthly token budget. **ADA works here with no UI at all.** When no provider key is configured the server falls back to `EchoLlmProvider`, so the pipeline is runnable and testable without a credential.
4. **Blazor UI — COMPLETE.** `Chat.razor` in `UI-Shared`: streaming reply, Stop, New chat, per-turn token display, and typed error handling for auth failure and an unreachable server. Both heads compose `AddAdaClient()` + `AddAdaSharedUi()`. Verified end to end in the browser, and the MAUI Windows app was launched and confirmed to render the identical shared UI.
   Sign-in landed later with accounts: the shared login page plus `SecureStorageSessionStore` on MAUI and a circuit-scoped store on the web head.
5. **Voice — COMPLETE, but unproven end to end.** `ISpeechToTextService`/`ITextToSpeechService` and `VoiceSessionState` in Core; Web Speech recognition and synthesis in the Web head; MAUI synthesis via Essentials and recognition via **Whisper.net** behind the swappable `ISpeechRecognitionEngine`; push-to-talk with barge-in in the shared UI; a settings page toggling voice and choosing the engine; Android `RECORD_AUDIO` + `<queries>` and the Windows microphone capability. **No audio has ever been transcribed** — the harness blocks microphone capture, so only synthesis and the error paths are verified. Server-side ElevenLabs remains deferred and opt-in.
6. **Android head — COMPLETE apart from running it.** Manifest permissions and `ApplicationId` landed earlier; this phase added the runtime-configurable server address (`IServerAddressProvider` plus a field on the login screen) and an Android network security config that keeps cleartext HTTP off everywhere except the named development hosts. **The app has never been run on Android** — this machine has no emulator, system image or connected device, so only the build and the APK contents are verified.
7. **Deployment — artifacts ready, not yet deployed.** `deploy/` holds the systemd units, an Apache vhost, an environment-file template and a runbook, written for Ubuntu + Apache. Nothing has been run on a real VPS. Note the vhost serves one hostname and splits `/api` and `/health` to the API with everything else to the web head, so a phone has a single address to type.
8. **Records, calendar and memories — Stage 1 COMPLETE.** Notes, calendar events with recurrence, and memories: entities and account-scoped SQL stores in `Data`, endpoints in `Server`, HTTP stores in Core, and a dashboard that takes over `/` (chat moved to `/chat`) alongside full CRUD pages. Per-user isolation was re-verified for all three types the way conversations were. Recurrence expands at query time and is **zone-aware** — see the note below. Stages 2 (tool-calling, so ADA creates these itself) and 3 (Android reminders) are not started.
9. **Tool-calling — Stage 2 COMPLETE.** `IAssistantTool` and `IToolRegistry` exist at last, and `AssistantPipeline` runs a bounded model→tool→model loop so ADA creates notes, events and memories itself. The system prompt is grounded with the user's local time and their most recent memories. Verified end to end with no paid credential, via a `!tool` marker in `EchoLlmProvider`. **The OpenAI tool path has never run against a real model** — there is no key on this machine, so only the Echo path is proven.
10. **Discord.** Convert to a generic-host worker; slash commands → HTTP client → Server. Nothing depends on it; it is the proof that the client/server split is clean.

## Risks and standing rules

- **Auth.** Per-device long random bearer token, stored **hashed** with a friendly name for revocation, TLS only, plus `AddRateLimiter` and fail2ban. Full OAuth/Identity is weeks of work for a user count of one; the upgrade path is documented, not built.
- **Public exposure.** The API is internet-facing by choice. Bind SQL Server to localhost only, keep the reverse proxy as the sole ingress, patch aggressively.
- **EF migrations.** Migrations live in `Data` with `--startup-project ADA-MKII-Server`. **Never `EnsureCreated()`. Never auto-migrate on startup in production** — generate a migrations bundle and run it as a deliberate deploy step. `Microsoft.EntityFrameworkCore.Design` is referenced with `PrivateAssets="all"` in **both** `Data` and `Server`: the tooling looks for it in the startup project, and `PrivateAssets` stops it flowing from `Data`. Generated migration files are marked `generated_code = true` in `.editorconfig`, because they trip CA1825/CA1861 and are rewritten on every `migrations add`.
- **Cost control** is implemented in `AssistantPipeline`: `llm.maxTokensPerTurn`, `llm.historyMessageLimit`, persisted `TokensIn`/`TokensOut`, and `cost.monthlyTokenBudget` checked *before* the model call. The budget is denominated in **tokens, not currency** — counts are exact and provider-independent, whereas a hardcoded price table silently goes stale and differs per model. Keep ElevenLabs off by default for the same reason. Note the chat `HttpClient` deliberately has **no resilience handler**: retrying a partly-consumed stream would pay twice for the same tokens.
- **The record tools live in Core, not `ADA-MKII-API`.** `ADA-MKII-API` means *clients we call* — outbound. `CreateNoteTool` and friends reach nothing off this machine; each is a thin adapter over a Core store abstraction, sitting beside the pipeline that dispatches them. Putting them in API would have re-created the exact naming confusion the top of this file warns about. A weather or ElevenLabs tool is outbound and does belong in API.
- **The tool loop is bounded at 4 iterations, and every iteration is a billed model call.** On the final iteration the tool list is sent empty, which forces prose rather than another call that would have nowhere to go. Usage is summed across iterations, not overwritten, or the monthly budget would only ever count the last one.
- **Tool results are model input, so they are message bodies.** `ToolRegistry` logs the tool name and never its arguments or result, for the same reason messages are not logged. Memories are the most sensitive text in the system.
- **A backend that ignores tools still works** — it simply never asks for one. For the rarer backend that *rejects* a request carrying tools, `llm.toolsEnabled` turns the offer off entirely rather than leaving chat broken.
- **Recurrence is expanded in a stored time zone, not an offset.** A `DateTimeOffset` records the offset that applied on the *first* occurrence, so stepping one forward keeps that offset and a 09:00 meeting silently becomes 08:00 once the zone leaves daylight saving. `CalendarEventEntity.TimeZoneId` holds the zone, and `RecurrenceExpander` steps in wall-clock time and recomputes the offset per occurrence. Both directions are verified against the 2026 New Zealand transitions. The zone comes from the **browser** (`Intl.DateTimeFormat().resolvedOptions().timeZone`, via `TimeZoneState`), never `TimeZoneInfo.Local` — on the web head that is the VPS, which is almost certainly not where the user is. A null zone means UTC.
- **Privacy.** The VPS will hold a verbatim transcript of everything ever said to ADA. Encrypt the volume, restrict SQL to localhost, and test restores.
- **Web Speech reality.** Effectively Chrome/Edge only, and Chrome on Linux ships audio to Google's servers. The server-side fallback is not optional — it is the default for Firefox users. Browser mic access also requires a **secure context**, so HTTPS is mandatory even in testing.
- **Blazor Hybrid tradeoff.** The sample's XAML styles become unused; only icons, splash and fonts survive, and fonts must be re-declared in CSS as well as `ConfigureFonts`. Costs: no native look-and-feel, higher WebView memory, slower Android cold start. Accepted in exchange for one UI across three platforms — do not spend time polishing MAUI XAML.
- **Single point of failure.** Server down means every head is dead, since there is no offline mode. Mitigate with health checks and systemd `Restart=always`; revisit a local read-cache if this bites.
- **Streaming transport is SSE, not SignalR.** SSE works identically from `HttpClient` (Discord), a Blazor Server circuit, and browser `fetch`. SignalR would add a second real-time stack on top of the Blazor circuit for no gain.
- **Two ASP.NET processes on one VPS** (`Server` + `Web`) is deliberate: it makes "no head may reference Data" mechanically true rather than a convention. The cost is one extra systemd unit and a localhost hop.
