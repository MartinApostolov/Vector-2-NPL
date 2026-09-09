param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipRestore,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'Vector.sln'
$extensionProject = Join-Path $repositoryRoot 'src\Vector.VisualStudio\Vector.VisualStudio.csproj'
$targetFramework = 'net8.0-windows8.0'
$vsix = Join-Path $repositoryRoot "src\Vector.VisualStudio\bin\$Configuration\$targetFramework\Vector.VisualStudio.vsix"

function Invoke-DotNet {
    param([string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipRestore) {
    Invoke-DotNet -Arguments @('restore', $solution)
}

Invoke-DotNet -Arguments @('build', $solution, '--no-restore', '--configuration', $Configuration, '--verbosity', 'minimal')

if (-not $SkipTests) {
    Invoke-DotNet -Arguments @('test', $solution, '--no-build', '--no-restore', '--configuration', $Configuration, '--verbosity', 'minimal')
}

Invoke-DotNet -Arguments @('build', $extensionProject, '--no-restore', '--configuration', $Configuration, '--verbosity', 'minimal')

if (-not (Test-Path -LiteralPath $vsix -PathType Leaf)) {
    throw "The expected VSIX was not produced at '$vsix'."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($vsix)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $requiredEntries = @(
        '.vsextension/extension.json',
        '.vsextension/settingsRegistration.json',
        '.vsextension/string-resources.json',
        'Grammars/vector.tmLanguage.json',
        'LanguageConfiguration/vector-language-configuration.json',
        'LanguageServer/Vector.LanguageServer.exe',
        'LanguageServer/Vector.Analysis.dll',
        'LanguageServer/Vector.Core.dll',
        'ExecutionHost/Vector.ExecutionHost.exe',
        'ExecutionHost/Vector.ExecutionProtocol.dll',
        'ExecutionHost/Vector.Core.dll',
        'ExecutionHost/Vector.Plugins.dll'
    )

    $missing = @($requiredEntries | Where-Object { $_ -notin $entries })
    if ($missing.Count -gt 0) {
        throw "VSIX payload is missing required entries: $($missing -join ', ')"
    }
}
finally {
    $archive.Dispose()
}

$hash = Get-FileHash -LiteralPath $vsix -Algorithm SHA256
Write-Host "Verified Vector Visual Studio VSIX: $vsix"
Write-Host "SHA256: $($hash.Hash)"
