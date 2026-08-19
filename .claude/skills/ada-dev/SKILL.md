---
name: ada-dev
description: Repeatable development operations for the ADA-MKII solution - EF Core migrations, running the Server and Web heads, smoke-testing the API with a device token, and verifying the project layering rules hold. Use this skill whenever working in the ADA-MKII repo and the task involves adding or applying a database migration, changing the DbContext or entities, starting or testing the server or web head, adding a ProjectReference or package, or checking that no client head has picked up EF Core or a provider SDK. Also use it when a build fails with CA1848, CA1825, CA1861, MSB4057, or an EF design-package error, since those have known repo-specific causes and fixes documented here.
---

# ADA-MKII development operations

This skill records the commands and gotchas for working in this solution. The
architecture itself — the project graph, layering rules, and design decisions —
lives in `CLAUDE.md` at the repo root. **Read `CLAUDE.md` first for *what* the
system is; use this skill for *how* to operate on it.**

Run every command from the repo root.

## Build and verify

```bash
dotnet build ADA-MKII.slnx
```

The whole solution builds with **zero warnings**, and `Directory.Build.props`
sets `TreatWarningsAsErrors=true`. Treat any new warning as a build break, not
noise — the zero-warning baseline is what makes a real problem visible. Style
rules are deliberately not enforced at build time; run `dotnet format` for those.

Building the full solution includes the Android target and takes several minutes.
For a fast inner loop, build the affected project alone, or the Windows target of
the MAUI head:

```bash
dotnet build ADA-MKII-UI/ADA-MKII-UI.csproj -f net10.0-windows10.0.19041.0
```

## Verify the layering rules

The single most valuable check in this repo. The architecture's core promise is
that no client head can reach the database or a provider SDK — so a careless
`ProjectReference` is the failure mode that matters most, and it is invisible to
the compiler.

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/ada-dev/scripts/check-layering.ps1
```

Run it after adding any `ProjectReference` or `PackageReference`. It exits
non-zero on a violation, so it also works as a CI gate. It asserts that
`ADA-MKII-UI`, `ADA-MKII-Web`, `ADA-MKII-Discord` and `ADA-MKII-UI-Shared` pull
no `EntityFrameworkCore`, `SqlClient`, `OpenAI` or `ElevenLabs` package, even
transitively.

If it fails, the fix is essentially never to suppress the check. Either the
reference belongs on `ADA-MKII-Server` instead, or the functionality belongs
behind an abstraction in `ADA-MKII-Core`.

## EF Core migrations

Migrations live in `ADA-MKII-Data`, but the tooling must be pointed at
`ADA-MKII-Server` as the startup project, because that is where the DI
registration and connection string are.

```bash
dotnet ef migrations add <Name> --project ADA-MKII-Data --startup-project ADA-MKII-Server
```

```bash
dotnet ef migrations list --project ADA-MKII-Data --startup-project ADA-MKII-Server
```

```bash
dotnet ef database update --project ADA-MKII-Data --startup-project ADA-MKII-Server
```

To undo a migration that has not been applied, use
`dotnet ef migrations remove` with the same two flags rather than deleting the
files by hand — the model snapshot has to stay in step.

**Never call `EnsureCreated()`, and never auto-migrate on startup in
production.** Generate a bundle and run it as a deliberate deploy step:

```bash
dotnet ef migrations bundle --project ADA-MKII-Data --startup-project ADA-MKII-Server
```

Three things about migrations in this repo that are not obvious:

- `Microsoft.EntityFrameworkCore.Design` is referenced by **both**
  `ADA-MKII-Data` and `ADA-MKII-Server`, each with `PrivateAssets="all"`. The
  tooling looks for it in the *startup* project, and `PrivateAssets` correctly
  stops it flowing from Data — so it has to be declared in both places.
- Generated migration files are marked `generated_code = true` in
  `.editorconfig`. They trip CA1825 and CA1861, and they are rewritten on every
  `migrations add`, so hand-fixing them would not survive.
- After changing entities or `AdaDbContext`, add a migration. A model change
  without one produces a runtime failure at the first query, not a build error.

## Running the heads

Two ASP.NET processes with deliberately separated ports, both defined in
`.claude/launch.json`:

| Head | Name | HTTP | HTTPS |
|---|---|---|---|
| `ADA-MKII-Server` (the API) | `server` | 5100 | 7100 |
| `ADA-MKII-Web` (Linux/browser UI) | `web` | 5200 | 7200 |

Prefer the preview tooling over a raw `dotnet run` so the process is managed and
its logs are readable:

- Start by name (`server` or `web`), then read logs if it misbehaves.
- Local development points at `(localdb)\MSSQLLocalDB`, configured in
  `ADA-MKII-Server/appsettings.Development.json`. That connection string uses
  trusted auth and holds no secret.

Develop UI against the **web head first**. It hot-reloads and needs no device
deploy, and because the components live in `ADA-MKII-UI-Shared`, whatever works
there works in the MAUI `BlazorWebView` too.

### Running the Android head on an emulator

```bash
dotnet build ADA-MKII-UI/ADA-MKII-UI.csproj -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
```

**`-p:EmbedAssembliesIntoApk=true` is not optional if you install the APK
yourself.** A Debug build otherwise uses Fast Deployment, which leaves the
managed assemblies *out* of the package and expects `dotnet build -t:Run` to push
them to the device separately. Install that APK by hand — with `adb install` or
any MCP tool — and the app dies on launch, before any of your code runs:

```
F/monodroid: No assemblies found in '/data/user/0/io.github.justiphi.ada/files/.__override__/x86_64'.
             Assuming this is part of Fast Deployment. Exiting...
```

It looks like a signing or architecture fault and is neither. The flag takes the
APK from ~15 MB to ~93 MB, which is the tell that the assemblies are now in it.
Do not add `AndroidFastDeploymentType` — it is deprecated and warns.

Then install and launch. The emulator reaches a server on the host loopback at
**`10.0.2.2`**, which is already the app's default and is one of the two hosts
allowed cleartext by `network_security_config.xml`:

```bash
adb install -r ADA-MKII-UI/bin/Debug/net10.0-android/io.github.justiphi.ada-Signed.apk
```

The launcher activity is `io.github.justiphi.ada/crc646cd1607415a9f99c.MainActivity`
— the CRC prefix is generated, so resolve it rather than hardcoding it:

```bash
adb shell cmd package resolve-activity --brief io.github.justiphi.ada
```

Accounts still come only from DataManager, so create one there before signing in;
`/api/auth/login` has no registration path by design.

Checking reminders without waiting for one to arrive:

```bash
adb shell "dumpsys alarm | grep -A2 ScheduledAlarmReceiver"
```

A scheduled reminder shows as an `RTC_WAKEUP` against
`plugin.LocalNotification.ScheduledAlarmReceiver` at `start - ReminderMinutesBefore`.
Its `window=` value is the inexact slack, which is routinely minutes and can be
tens of minutes.

## Smoke-testing the API

The server requires a device bearer token on everything except `/health`.

A fresh database has no tokens, so seed one via `Ada:Auth:BootstrapToken`:

```bash
dotnet user-secrets set "Ada:Auth:BootstrapToken" "<a long random string>" --project ADA-MKII-Server
```

The server registers it at startup under `Ada:Auth:BootstrapTokenName`. Apply
migrations *before* first run — seeding writes to the database and fails fast if
the schema is absent.

Mint tokens with `DeviceTokens.Generate()` in `ADA-MKII-Core`. If generating one
in a shell instead, be careful on Windows PowerShell 5.1:
`[RandomNumberGenerator]::Fill()` does not exist there and fails in a way that
can silently leave an all-zero buffer. Use
`[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)`
and sanity-check the result has more than one distinct byte value.

Expected behaviour, worth re-checking after any auth change:

| Request | Expected |
|---|---|
| `GET /health`, no credential | 200 |
| `GET /api/conversations`, no credential | 401 |
| `GET /api/conversations`, unknown or revoked token | 401 |
| `GET /api/conversations`, valid token | 200 |
| Invalid request body | 400 with an RFC 9457 ProblemDetails payload |

Tokens are stored as SHA-256 hashes, never in plaintext. A quick way to confirm
that property still holds is to read the `DeviceTokens` table and check
`LEN(TokenHash)` is 64 and the stored value does not equal the token you sent.

## Known build failures and their causes

These have all bitten in this repo. The message rarely points at the real cause.

**`CA1848: use LoggerMessage delegates`** — logging must use source-generated
`[LoggerMessage]` partial methods, not `logger.LogInformation(...)`. This is
enforced, not advisory: it keeps every message template in one auditable place,
which matters because message bodies and tokens must never be logged. Follow the
pattern in `ADA-MKII-Server/Logging/ServerLog.cs`.

**`CA1825` / `CA1861` in a `Migrations/` file** — the `.editorconfig` rule that
marks migrations as generated is missing or the file sits outside a `Migrations/`
directory. Do not edit the generated file.

**`CA1707: remove the underscores from namespace name`** — the `ADA_MKII_*` root
namespaces are deliberate, since hyphens are illegal in namespaces. CA1707 is
suppressed in `.editorconfig`. If it reappears, the suppression was lost; do not
rename the namespaces.

**`MSB4057: the target "StaticWebAssetsPrepareForRun" does not exist`** — a MAUI
Blazor Hybrid project needs `Microsoft.NET.Sdk.Razor`, not the base
`Microsoft.NET.Sdk`. Check the `<Project Sdk="...">` line in
`ADA-MKII-UI.csproj`.

**`CA1416: only supported on 'android' 24.0 and later`** — `AddMauiBlazorWebView()`
requires Android API 24. `SupportedOSPlatformVersion` for android must be `24.0`,
not the MAUI template default of `21.0`.

**Every route 404s in the web head, but it compiles** — `MapRazorComponents<App>()`
only discovers routable pages in its own assembly. Pages live in
`ADA-MKII-UI-Shared`, so `Program.cs` must keep
`.AddAdditionalAssemblies(typeof(ADA_MKII_UI_Shared.Routes).Assembly)`.

**`Your startup project doesn't reference Microsoft.EntityFrameworkCore.Design`** —
see the migrations section above; it belongs in `ADA-MKII-Server` too.

**`APT2000: ... res.zip: No such file or directory`** — stale Android
intermediates, usually after a TFM or package change. Nothing is wrong with the
code. Delete `ADA-MKII-UI/obj` and `ADA-MKII-UI/bin` and rebuild.

**`CA2024: do not use 'reader.EndOfStream' in an async method`** — it blocks the
thread and stalls on an open-but-idle stream, which is exactly what an SSE
connection is. Loop on the read instead:
`while (await reader.ReadLineAsync(ct) is { } line)`.

**`MSB3027: could not copy … locked by ADA-MKII-Web`** — a head is still running
from a previous preview. Stop it before rebuilding.

**`XamlParseException: No matching constructor found on type 'MainWindow'`** —
`App.xaml` still has `StartupUri`. A window with constructor injection must be
created in `App.OnStartup` after the service provider exists, and `StartupUri`
must be removed or WPF races ahead and news it up with no arguments.

**Requests go out unauthenticated even though the user is signed in** —
almost certainly a `DelegatingHandler` resolving a scoped service.
`IHttpClientFactory` builds handlers in the handler pool's own DI scope, not the
caller's, so a handler that injects `ISessionStore` gets a different instance
than the one login wrote to. Attach the token inside the typed client (see
`AuthenticatedHttpClient`), which *is* resolved from the calling scope.

## Managing accounts

`ADA-MKII-DataManager` is the only way to create an account — the API has no
registration endpoint. It connects straight to SQL Server, so run it only
somewhere trusted.

```bash
dotnet user-secrets set "ConnectionStrings:Ada" "<connection string>" --project ADA-MKII-DataManager
```

It reports connection state and pending migrations in its status bar at startup,
naming the server and database, so a wrong connection string or a schema
mismatch is visible before you touch anything.

**If it appears to hang, suspect the database first.** `AddAdaDataAdmin`
deliberately omits `EnableRetryOnFailure` and shortens the connect timeout,
because the server's retry defaults - 6 attempts at a 15-second timeout - turn an
unreachable database into more than a minute of motionless window. An
unreachable host should now be reported in a few seconds. If a future change
reintroduces retries here, that symptom comes straight back.

To check a login end to end afterwards:

```bash
curl -s -X POST http://localhost:5100/api/auth/login -H "Content-Type: application/json" -d '{"username":"someone","password":"..."}'
```

The response carries the bearer token **once**; only its hash is stored.

## Inspecting a WPF or MAUI window

The MAUI head is a desktop app, so browser tooling cannot see it. Build and run
it, then capture the window with `PrintWindow` and the `PW_RENDERFULLCONTENT`
flag (`2`) — plain `CopyFromScreen` grabs whatever is on top of the desktop, and
stealing focus is rude if something else is running fullscreen:

```
dotnet build ADA-MKII-UI/ADA-MKII-UI.csproj -f net10.0-windows10.0.19041.0
```

The executable lands in `ADA-MKII-UI/bin/Debug/net10.0-windows10.0.19041.0/win-x64/`.
A useful sanity check on a capture: count distinct pixel colours. A blank or
failed capture has one or two; a real render has dozens.

**Test both heads after any change to shared CSS.** The two link different
stylesheets — the web head also loads its own `wwwroot/app.css`, the MAUI head
loads only the shared `ada.css`. A rule present in one and missing from the other
produces a bug visible on exactly one head, which is how the always-visible
`#blazor-error-ui` banner slipped through.

## Testing the chat stream

`POST /api/chat` returns server-sent events, so a buffering client makes a
working stream look broken. Read with `HttpCompletionOption.ResponseHeadersRead`
and consume line by line; each event arrives as `data: {json}`.

With no provider key configured the server uses `EchoLlmProvider`, which streams
a canned reply with a small inter-word delay. That is the point: the whole
pipeline — streaming, persistence, token accounting, the spend guard — is
exercisable without a credential or a bill. Timestamps on arriving deltas are
the quickest way to confirm streaming is genuine rather than one delayed lump.

To exercise the **spend guard**, set the budget below what has already been used
this month and send a turn — it must be refused before any model call:

```bash
curl -X PUT http://localhost:5100/api/settings/cost.monthlyTokenBudget -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"value":"1"}'
```

Restore it afterwards. `0` disables the guard entirely.

## Adding a new package

Versions are managed centrally. Add a `<PackageVersion>` to
`Directory.Packages.props`, then reference it from the csproj **without** a
`Version` attribute:

```xml
<PackageReference Include="Some.Package" />
```

A `Version` on the `PackageReference` fails the build under central package
management. After adding anything to a client head, re-run the layering check.
