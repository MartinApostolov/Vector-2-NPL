param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipRestore,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'Vector.sln'
$packageProject = Join-Path $repositoryRoot 'src\Vector.VisualStudio.Package\Vector.VisualStudio.Package.csproj'
$vsix = Join-Path $repositoryRoot "src\Vector.VisualStudio.Package\bin\$Configuration\net472\Vector.VisualStudio.Package.vsix"

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

Invoke-DotNet -Arguments @('build', $packageProject, '--no-restore', '--configuration', $Configuration, '--verbosity', 'minimal')

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
        'Vector.LanguageConfiguration.pkgdef',
        'Grammars/vector.tmLanguage.json',
        'LanguageConfiguration/vector-language-configuration.json',
        'OutOfProc/Vector.VisualStudio.dll',
        'OutOfProc/LanguageServer/Vector.LanguageServer.exe',
        'OutOfProc/LanguageServer/Vector.Analysis.dll',
        'OutOfProc/LanguageServer/Vector.Core.dll',
        'OutOfProc/ExecutionHost/Vector.ExecutionHost.exe',
        'OutOfProc/ExecutionHost/Vector.ExecutionProtocol.dll',
        'OutOfProc/ExecutionHost/Vector.Core.dll',
        'OutOfProc/ExecutionHost/Vector.Plugins.dll'
    )

    $missing = @($requiredEntries | Where-Object { $_ -notin $entries })
    if ($missing.Count -gt 0) {
        throw "VSIX payload is missing required entries: $($missing -join ', ')"
    }

    $manifestEntry = $archive.GetEntry('extension.vsixmanifest')
    $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml]$manifest = $manifestReader.ReadToEnd()
    }
    finally {
        $manifestReader.Dispose()
    }

    $installation = $manifest.PackageManifest.Installation
    if ($installation.ExtensionType -ne 'VSSDK+VisualStudio.Extensibility') {
        throw "VSIX must be hybrid so Visual Studio processes its TextMate pkgdef registration."
    }

    $extensionEntry = $archive.GetEntry('.vsextension/extension.json')
    $extensionReader = [System.IO.StreamReader]::new($extensionEntry.Open())
    try {
        $extensionMetadata = $extensionReader.ReadToEnd() | ConvertFrom-Json
    }
    finally {
        $extensionReader.Dispose()
    }

    $invalidServices = @($extensionMetadata.services | Where-Object {
        $_.serviceBaseDirectory -ne '.\OutOfProc' -or $_.entryPoint.assemblyPath -ne 'Vector.VisualStudio.dll'
    })
    if ($invalidServices.Count -gt 0) {
        throw 'VisualStudio.Extensibility services must load out of the isolated OutOfProc folder.'
    }
}
finally {
    $archive.Dispose()
}

$hash = Get-FileHash -LiteralPath $vsix -Algorithm SHA256
Write-Host "Verified Vector Visual Studio VSIX: $vsix"
Write-Host "SHA256: $($hash.Hash)"
