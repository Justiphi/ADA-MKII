<#
.SYNOPSIS
    Asserts the ADA-MKII layering rules hold.

.DESCRIPTION
    The architecture's central promise is that no client head can reach the
    database or a third-party provider SDK - all of that traffic goes through
    ADA-MKII-Server. That promise is invisible to the compiler: a single stray
    ProjectReference breaks it and everything still builds.

    This script checks the transitive package closure of each head and fails if
    a forbidden package appears. It exits non-zero on violation so it can be
    used as a CI gate.

.EXAMPLE
    powershell -File .claude/skills/ada-dev/scripts/check-layering.ps1
#>

[CmdletBinding()]
param(
    # Repo root. Defaults to four levels up from this script.
    [string] $RepoRoot
)

$ErrorActionPreference = 'Stop'

# Resolved here rather than as a param default: in Windows PowerShell 5.1
# $PSScriptRoot is not yet populated while param defaults are evaluated.
if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
}

# Heads must reach the server over HTTP only - never the data or provider layers.
$heads = @(
    'ADA-MKII-UI',
    'ADA-MKII-Web',
    'ADA-MKII-Discord',
    'ADA-MKII-UI-Shared'
)

# Packages that prove a head has reached past ADA-MKII-Server.
$forbidden = 'EntityFrameworkCore|SqlClient|OpenAI|ElevenLabs'

$violations = @()

foreach ($head in $heads) {
    $csproj = Join-Path $RepoRoot "$head\$head.csproj"

    if (-not (Test-Path $csproj)) {
        Write-Warning "$head : project not found at $csproj - skipped"
        continue
    }

    # No 2>&1 here: in Windows PowerShell 5.1 redirecting a native command's
    # stderr wraps each line in an ErrorRecord and trips ErrorActionPreference.
    $output = & dotnet list $csproj package --include-transitive | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet list failed for $head. Restore the solution and try again.`n$output"
    }

    $hits = ($output -split "`r?`n") | Select-String -Pattern $forbidden

    if ($hits) {
        Write-Host "$head : VIOLATION" -ForegroundColor Red
        foreach ($hit in $hits) {
            $line = $hit.ToString().Trim()
            Write-Host "    $line" -ForegroundColor Red
            $violations += "$head -> $line"
        }
    }
    else {
        Write-Host "$head : clean" -ForegroundColor Green
    }
}

Write-Host ""

if ($violations.Count -gt 0) {
    Write-Host "Layering check FAILED with $($violations.Count) violation(s)." -ForegroundColor Red
    Write-Host "A head has reached past ADA-MKII-Server. The fix is almost never to"
    Write-Host "suppress this: either move the reference onto ADA-MKII-Server, or put"
    Write-Host "the functionality behind an abstraction in ADA-MKII-Core."
    exit 1
}

Write-Host "Layering check passed: no head pulls EF Core, SQL Server or a provider SDK." -ForegroundColor Green
exit 0
