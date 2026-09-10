[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$extensionRoot = Join-Path $repositoryRoot 'src\Vector.VSCode'
$serverRoot = Join-Path $extensionRoot 'server'
$languageServerOutput = Join-Path $serverRoot 'language-server'
$executionHostOutput = Join-Path $serverRoot 'execution-host'

foreach ($target in @($languageServerOutput, $executionHostOutput)) {
    $resolvedParent = [IO.Path]::GetFullPath((Split-Path -Parent $target))
    $resolvedServerRoot = [IO.Path]::GetFullPath($serverRoot)
    if (-not $resolvedParent.StartsWith($resolvedServerRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace payload outside '$resolvedServerRoot': '$target'."
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

dotnet publish `
    (Join-Path $repositoryRoot 'src\Vector.LanguageServer\Vector.LanguageServer.csproj') `
    --configuration $Configuration `
    --no-restore `
    --no-self-contained `
    -p:UseAppHost=false `
    --output $languageServerOutput
if ($LASTEXITCODE -ne 0) {
    throw "Vector.LanguageServer publish failed with exit code $LASTEXITCODE."
}

dotnet publish `
    (Join-Path $repositoryRoot 'src\Vector.ExecutionHost\Vector.ExecutionHost.csproj') `
    --configuration $Configuration `
    --no-restore `
    --no-self-contained `
    -p:UseAppHost=false `
    --output $executionHostOutput
if ($LASTEXITCODE -ne 0) {
    throw "Vector.ExecutionHost publish failed with exit code $LASTEXITCODE."
}

foreach ($requiredFile in @(
    (Join-Path $languageServerOutput 'Vector.LanguageServer.dll'),
    (Join-Path $languageServerOutput 'Vector.LanguageServer.runtimeconfig.json'),
    (Join-Path $executionHostOutput 'Vector.ExecutionHost.dll'),
    (Join-Path $executionHostOutput 'Vector.ExecutionHost.runtimeconfig.json')
)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required VS Code payload file is missing: '$requiredFile'."
    }
}

Write-Host "Vector VS Code managed payload built at: $serverRoot"
