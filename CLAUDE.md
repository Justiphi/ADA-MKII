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
| `ADA-MKII-Data` | `Microsoft.NET.Sdk` | `net10.0` | EF Core + SQL Server, `AdaDbContext`, entities, migrations. **Server-only.** |
| `ADA-MKII-Server` | `Microsoft.NET.Sdk.Web` | `net10.0` | Minimal API hosted on the VPS. The only process holding secrets. |
| `ADA-MKII-UI-Shared` | `Microsoft.NET.Sdk.Razor` | `net10.0` | Razor Class Library — every component, page and CSS. Shared by both UI heads. |
| `ADA-MKII-UI` | MAUI | `net10.0-android;net10.0-windows10.0.19041.0` | Blazor Hybrid host + device STT/TTS/SecureStorage. |
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
dotnet list ADA-MKII-UI/ADA-MKII-UI.csproj package --include-transitive
```

An EF Core or provider-SDK hit in `UI`, `Web` or `Discord` is a layering violation, not a curiosity.

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
3. **`Data` and `API` may be referenced only by `Server`.**
4. **No head (`UI`, `Web`, `Discord`) may reference `Data`, `API`, or `Server`.**
5. **`UI-Shared` contains no platform-conditional code** (`#if ANDROID` / `#if WINDOWS`) and **must not reference `Microsoft.Maui.*`**. If it does, the Web head stops compiling — that is the enforcement mechanism, and it is a feature.
6. **Only `Server` reads provider secrets.** Heads hold exactly one secret: the device bearer token.
7. **EF entities are internal to `Data`** and never cross the wire. Stores return Core DTOs.
8. **Implementations are named `<Provider><Interface>`:** `OpenAiLlmProvider`, `SqlConversationStore`, `HttpConversationStore`, `MauiSpeechToTextService`, `WebSpeechToTextService`.

## Key abstractions

All interfaces live in `ADA_MKII_Core.Abstractions`.

| Interface | Shape | Implementations |
|---|---|---|
| `ILlmProvider` | `IAsyncEnumerable<LlmDelta> StreamAsync(LlmRequest, CancellationToken)` | `OpenAiLlmProvider` (API), `FakeLlmProvider` (tests) |
| `IAssistantTool` | `Name`, `JsonSchema`, `Task<ToolResult> InvokeAsync(JsonElement, CancellationToken)` | `WeatherTool`, `TimeTool` (API) |
| `IToolRegistry` | Discovery + dispatch of `IAssistantTool` | Core |
| `IIntentDispatcher` | `Task<CommandResult?> TryHandleAsync(...)` — deterministic commands before the LLM | Core |
| `IAssistantPipeline` | `IAsyncEnumerable<ChatStreamEvent> RunAsync(ChatRequest, CancellationToken)` — the one entry point | Core (composed server-side only) |
| `IConversationStore` | `Create` / `Get` / `List` / `AppendMessage` | `SqlConversationStore` (Data) **and** `HttpConversationStore` (Core) |
| `ISettingsStore` | `GetAsync<T>(key, ct)` / `SetAsync(key, value, ct)` | `SqlSettingsStore` (Data), `HttpSettingsStore` (Core) |
| `ISecretStore` | Device token only | `SecureStorageSecretStore` (MAUI), config-backed (Web), env-backed (Discord) |
| `ISpeechToTextService` | `RequestPermissionAsync`, `IAsyncEnumerable<SpeechPartial> ListenAsync(CultureInfo, ct)` | `MauiSpeechToTextService`, `WebSpeechToTextService`, `NullSpeechToTextService` |
| `ITextToSpeechService` | `SpeakAsync(text, options, ct)`, `StopAsync()` | `MauiTextToSpeechService`, `WebSpeechSynthesisService`, `ElevenLabsTtsService` (API) |
| `IWeatherProvider` | Typed integration seam | API |

**Critical design note:** `IConversationStore` has both a `Sql*` implementation (Data, server-side) and an `Http*` implementation (Core, client-side). That symmetry is what lets `UI-Shared` be byte-identical across every head.

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

**Client token storage:** MAUI → `SecureStorage`; Web head → server-side config, never sent to the browser; Discord → environment variable.

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

**Logging.** `Microsoft.Extensions.Logging` abstractions everywhere; inject `ILogger<T>`, never use a static logger. Server adds Serilog → console (journald) + rolling file, with `ConversationId` in a log scope per turn. **Never log message bodies at Information level. Never log keys or tokens** — add a redaction rule.

**Cancellation.** Sources per head: `HttpContext.RequestAborted` (Server), component disposal (Blazor), page lifecycle plus an explicit stop button (MAUI voice). Streaming endpoints must honour client disconnect so a cancelled chat stops burning tokens.

**Error handling.**
- Server returns RFC 9457 `ProblemDetails` for every failure (`AddProblemDetails()` + exception handler middleware). No raw exception text to clients.
- The HTTP client maps non-2xx to a typed `AdaApiException(ProblemDetails, HttpStatusCode)`.
- Outbound provider calls are wrapped in `Microsoft.Extensions.Http.Resilience` (retry with jitter, total timeout, circuit breaker) — this protects against provider flakiness and bounds cost during an outage.
- Never swallow exceptions. Never `catch { }`.

**Offline (mobile).** Phase 1 has **no offline cache.** The MAUI head watches `Connectivity.Current.NetworkAccess` and shows an explicit offline banner with the composer disabled. A cache without an outbox is a lie, and an outbox needs conflict rules this app does not yet have. Consequently, `Microsoft.Data.Sqlite.Core` and `SQLitePCLRaw.bundle_green` are to be **removed** from `ADA-MKII-UI.csproj`.

## Voice pipeline

**Flow:** mic → STT partials stream into the composer → final transcript → `POST /api/chat` (SSE) → assistant deltas render → completed text → `ITextToSpeechService.SpeakAsync`.

Push-to-talk only. **No wake word** in phase 1 — the battery and false-trigger cost is not yet worth it. **Barge-in:** starting a listen must call `StopAsync()` on TTS first.

`UI-Shared` sees only the two interfaces plus a `VoiceSessionState` enum (`Idle | Listening | Thinking | Speaking`). The head supplies the implementation.

| Head | STT | TTS |
|---|---|---|
| MAUI (Win/Android) | `CommunityToolkit.Maui.Media.SpeechToText.Default` — requires `.UseMauiCommunityToolkit()` | `Microsoft.Maui.Media.TextToSpeech.Default` |
| Web / Linux | JS interop module wrapping `webkitSpeechRecognition`, partials returned via `DotNetObjectReference` | `window.speechSynthesis` |
| Web fallback (Firefox) | `MediaRecorder` → `POST /api/speech/stt` | `/api/speech/tts` → `audio/mpeg` in an `<audio>` element |
| Discord | `NullSpeechToTextService` | `NullTextToSpeechService` |

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
| `ApplicationId` | **Change** `com.companyname.adamkiiui` to a real reverse-DNS id **before any Android install** — changing it later orphans installed app data. |
| `Syncfusion.Maui.Toolkit` | **Remove.** XAML-only, therefore dead weight under Blazor Hybrid, and it requires a registered licence key at runtime. |
| Testing | xUnit v3 + NSubstitute + Shouldly in a `tests/` folder. Deferred — not part of the initial 8-project graph. **Avoid FluentAssertions**, v8+ is commercially licensed. |

## Build-out sequence

Critical path is **0 → 1 → 2 → 3 → 4**. Voice, Android and Discord are leaves.

0. **Repo hygiene — COMPLETE.** This file plus `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, `nuget.config`; hoisted properties and package versions out of all five csprojs. The four non-MAUI projects build clean with zero warnings under warnings-as-errors. *Set the compiler policy all later code is written under.*
1. **Skeleton.** Create `Server`, `Web`, `UI-Shared`; wire every `ProjectReference`; strip the MAUI sample (delete `Pages/`, `PageModels/`, `Models/`, `Data/`, `Utilities/`, `Services/`; keep `Resources/`); retarget TFMs; drop Syncfusion and the SQLite packages; add `Microsoft.AspNetCore.Components.WebView.Maui`. Everything compiles, nothing does anything yet.
   **Note:** dropping `SQLitePCLRaw.bundle_green` also clears a live `NU1903` high-severity advisory on `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which the MAUI build currently reports. Once the sample is gone, remove the `TreatWarningsAsErrors=false` opt-out from `ADA-MKII-UI.csproj` — all ~140 current MAUI warnings live in sample code being deleted.
2. **Data + Server foundation.** Replace `Model.cs` with a top-level `AdaDbContext` (`Conversation`, `Message`, `Setting`); drop `APIKey`; swap EF Sqlite → SqlServer; connection string from config; first migration. Server gets `/health`, bearer auth, ProblemDetails, Serilog. *Blocks every head.*
3. **LLM end-to-end.** Core pipeline, `OpenAiLlmProvider` with streaming, `POST /api/chat` as SSE, the HTTP client. **ADA first works here, with no UI at all** — prove it with `curl`.
4. **Blazor UI.** Build the chat UI in `UI-Shared`. Bring it up on the **Web head first** (fast inner loop, hot reload, no device deploy), then host the identical RCL in `BlazorWebView` on Windows.
5. **Voice.** MAUI STT/TTS + Windows mic permission + the partial-result workaround; then Web Speech interop; then optional server-side ElevenLabs.
6. **Android head.** Manifest permissions, `ApplicationId`, reaching the VPS over HTTPS from a phone.
7. **Deployment.** SQL Server on the VPS, systemd units for Server and Web, reverse proxy + Let's Encrypt, `dotnet ef migrations bundle`, backups.
8. **Discord.** Convert to a generic-host worker; slash commands → HTTP client → Server. Nothing depends on it; it is the proof that the client/server split is clean.

## Risks and standing rules

- **Auth.** Per-device long random bearer token, stored **hashed** with a friendly name for revocation, TLS only, plus `AddRateLimiter` and fail2ban. Full OAuth/Identity is weeks of work for a user count of one; the upgrade path is documented, not built.
- **Public exposure.** The API is internet-facing by choice. Bind SQL Server to localhost only, keep the reverse proxy as the sole ingress, patch aggressively.
- **EF migrations.** Migrations live in `Data` with `--startup-project ADA-MKII-Server`. **Never `EnsureCreated()`. Never auto-migrate on startup in production** — generate a migrations bundle and run it as a deliberate deploy step. `Microsoft.EntityFrameworkCore.Design` must be `PrivateAssets="all"`.
- **Cost control — build this in from day one, not later.** Max-tokens cap per turn, conversation trimming/summarisation above N messages, persisted `TokensIn`/`TokensOut` per message row, a monthly spend guard read from `Setting`, ElevenLabs off by default. A runaway streaming loop with retries is the realistic way to get a surprise bill.
- **Privacy.** The VPS will hold a verbatim transcript of everything ever said to ADA. Encrypt the volume, restrict SQL to localhost, and test restores.
- **Web Speech reality.** Effectively Chrome/Edge only, and Chrome on Linux ships audio to Google's servers. The server-side fallback is not optional — it is the default for Firefox users. Browser mic access also requires a **secure context**, so HTTPS is mandatory even in testing.
- **Blazor Hybrid tradeoff.** The sample's XAML styles become unused; only icons, splash and fonts survive, and fonts must be re-declared in CSS as well as `ConfigureFonts`. Costs: no native look-and-feel, higher WebView memory, slower Android cold start. Accepted in exchange for one UI across three platforms — do not spend time polishing MAUI XAML.
- **Single point of failure.** Server down means every head is dead, since there is no offline mode. Mitigate with health checks and systemd `Restart=always`; revisit a local read-cache if this bites.
- **Streaming transport is SSE, not SignalR.** SSE works identically from `HttpClient` (Discord), a Blazor Server circuit, and browser `fetch`. SignalR would add a second real-time stack on top of the Blazor circuit for no gain.
- **Two ASP.NET processes on one VPS** (`Server` + `Web`) is deliberate: it makes "no head may reference Data" mechanically true rather than a convention. The cost is one extra systemd unit and a localhost hop.
