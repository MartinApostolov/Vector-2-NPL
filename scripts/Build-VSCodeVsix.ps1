[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$VSCodeExecutablePath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$extensionRoot = Join-Path $repositoryRoot 'src\Vector.VSCode'
$outputRoot = Join-Path $extensionRoot 'out'
$vsixPath = Join-Path $outputRoot 'vector-language-support-0.1.0.vsix'

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string]$Description,
        [Parameter(Mandatory)]
        [scriptblock]$Action
    )

    Write-Host "`n== $Description =="
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    Invoke-Checked 'Restore .NET solution' { dotnet restore Vector.sln }
    Invoke-Checked 'Build .NET solution' { dotnet build Vector.sln --configuration $Configuration --no-restore }
    Invoke-Checked 'Run all C# tests' { dotnet test Vector.sln --configuration $Configuration --no-build --no-restore }

    Push-Location $extensionRoot
    try {
        Invoke-Checked 'Restore Node dependencies' { npm.cmd ci }
        Invoke-Checked 'Build managed VS Code payloads' {
            & (Join-Path $PSScriptRoot 'Build-VSCodePayload.ps1') -Configuration $Configuration
        }
        Invoke-Checked 'Run VS Code unit and process tests' { npm.cmd test }

        if ([string]::IsNullOrWhiteSpace($VSCodeExecutablePath) -and
            [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
            $candidate = Join-Path $env:LOCALAPPDATA 'Programs\Microsoft VS Code\Code.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $VSCodeExecutablePath = $candidate
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($VSCodeExecutablePath)) {
            $resolvedCode = (Resolve-Path -LiteralPath $VSCodeExecutablePath).Path
            $env:VECTOR_VSCODE_EXECUTABLE = $resolvedCode
            Write-Host "Using VS Code Extension Development Host: $resolvedCode"
        }

        Invoke-Checked 'Run VS Code Extension Development Host tests' { npm.cmd run test:extension }
        Invoke-Checked 'Build production extension bundle' { npm.cmd run package:bundle }
        New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
        Invoke-Checked 'Package VSIX' {
            npx.cmd --no-install vsce package --no-dependencies --out $vsixPath
        }
    }
    finally {
        Pop-Location
    }

    & (Join-Path $PSScriptRoot 'Audit-VSCodeVsix.ps1') -VsixPath $vsixPath
    $hash = Get-FileHash -LiteralPath $vsixPath -Algorithm SHA256
    Write-Host "`nVector VS Code VSIX: $($hash.Path)"
    Write-Host "SHA-256: $($hash.Hash)"
}
finally {
    Pop-Location
}
