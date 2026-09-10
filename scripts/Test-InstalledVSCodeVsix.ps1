[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$VsixPath,
    [Parameter(Mandatory)]
    [string]$VSCodeExecutablePath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$extensionRoot = Join-Path $repositoryRoot 'src\Vector.VSCode'
$resolvedVsix = (Resolve-Path -LiteralPath $VsixPath).Path
$resolvedCode = (Resolve-Path -LiteralPath $VSCodeExecutablePath).Path
$codeCli = Join-Path (Split-Path -Parent $resolvedCode) 'bin\code.cmd'
if (-not (Test-Path -LiteralPath $codeCli -PathType Leaf)) {
    throw "VS Code command-line launcher not found at '$codeCli'."
}

$testRoot = Join-Path (Join-Path $extensionRoot 'out') ("installed-vsix-test-" + [Guid]::NewGuid().ToString('N'))
$profile = Join-Path $testRoot 'profile'
$extensions = Join-Path $testRoot 'extensions'
$resultPath = Join-Path $testRoot 'result.txt'
$harness = Join-Path $extensionRoot 'dist-test\test\installed\harness'
$harnessVsix = Join-Path $testRoot 'vector-installed-test-harness.vsix'
$workspace = Join-Path $extensionRoot 'test\fixtures\workspace'
$testDriver = Join-Path $extensionRoot 'scripts\test-installed.mjs'

try {
    New-Item -ItemType Directory -Force -Path $profile, $extensions | Out-Null
    Push-Location $harness
    try {
        npx.cmd --no-install vsce package --no-dependencies --out $harnessVsix
        if ($LASTEXITCODE -ne 0) {
            throw "Installed test harness packaging failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    & $codeCli --user-data-dir $profile --extensions-dir $extensions --install-extension $resolvedVsix --force
    if ($LASTEXITCODE -ne 0) {
        throw "VSIX installation failed with exit code $LASTEXITCODE."
    }
    & $codeCli --user-data-dir $profile --extensions-dir $extensions --install-extension $harnessVsix --force
    if ($LASTEXITCODE -ne 0) {
        throw "Installed test harness installation failed with exit code $LASTEXITCODE."
    }

    $env:VECTOR_INSTALLED_TEST_RESULT = $resultPath
    node $testDriver $resolvedCode $extensions $profile $workspace
    if ($LASTEXITCODE -ne 0) {
        throw "Installed VSIX test host failed with exit code $LASTEXITCODE."
    }

    for ($attempt = 1; $attempt -le 50 -and -not (Test-Path -LiteralPath $resultPath -PathType Leaf); $attempt++) {
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw 'Installed VSIX test host did not write a result.'
    }

    $result = Get-Content -Raw -LiteralPath $resultPath
    if (-not $result.StartsWith('PASS:', [StringComparison]::Ordinal)) {
        Get-ChildItem -LiteralPath (Join-Path $profile 'logs') -Recurse -File -Filter 'Vector.log' -ErrorAction SilentlyContinue |
            ForEach-Object { Get-Content -LiteralPath $_.FullName }
        throw "Installed VSIX test failed: $result"
    }

    Write-Host $result
}
finally {
    Remove-Item Env:VECTOR_INSTALLED_TEST_RESULT -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $testRoot) {
        $removed = $false
        for ($attempt = 1; $attempt -le 20 -and -not $removed; $attempt++) {
            try {
                Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction Stop
                $removed = $true
            }
            catch {
                if ($attempt -eq 20) {
                    throw
                }
                Start-Sleep -Milliseconds 500
            }
        }
        if (-not $removed) {
            throw "Unable to remove installed VSIX test directory '$testRoot'."
        }
    }
}
