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

## Adding a new package

Versions are managed centrally. Add a `<PackageVersion>` to
`Directory.Packages.props`, then reference it from the csproj **without** a
`Version` attribute:

```xml
<PackageReference Include="Some.Package" />
```

A `Version` on the `PackageReference` fails the build under central package
management. After adding anything to a client head, re-run the layering check.
