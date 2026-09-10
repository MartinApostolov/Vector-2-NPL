[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$VsixPath
)

$ErrorActionPreference = 'Stop'
$resolvedVsix = (Resolve-Path -LiteralPath $VsixPath).Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($resolvedVsix)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $required = @(
        'extension/package.json',
        'extension/README.md',
        'extension/dist/extension.js',
        'extension/language-configuration.json',
        'extension/syntaxes/vector.tmLanguage.json',
        'extension/server/language-server/Vector.LanguageServer.dll',
        'extension/server/language-server/Vector.LanguageServer.deps.json',
        'extension/server/language-server/Vector.LanguageServer.runtimeconfig.json',
        'extension/server/language-server/Vector.Analysis.dll',
        'extension/server/language-server/Vector.Core.dll',
        'extension/server/execution-host/Vector.ExecutionHost.dll',
        'extension/server/execution-host/Vector.ExecutionHost.deps.json',
        'extension/server/execution-host/Vector.ExecutionHost.runtimeconfig.json',
        'extension/server/execution-host/Vector.ExecutionProtocol.dll',
        'extension/server/execution-host/Vector.Plugins.dll',
        'extension/server/execution-host/Vector.Core.dll'
    )
    $missing = @($required | Where-Object { $_ -notin $entries })
    if ($missing.Count -gt 0) {
        throw "VSIX is missing required payload entries:`n$($missing -join "`n")"
    }

    $forbidden = @($entries | Where-Object {
        $_ -match '(^|/)(\.git|\.vs|\.vscode-test|node_modules|src|test|dist-test|out)(/|$)' -or
        $_ -match '\.(pdb|map|user|suo)$' -or
        $_ -match '(^|/)(package-lock\.json|tsconfig\.json)$'
    })
    if ($forbidden.Count -gt 0) {
        throw "VSIX contains forbidden development artifacts:`n$($forbidden -join "`n")"
    }

    $managedExecutables = @($entries | Where-Object { $_ -match '^extension/server/.+\.exe$' })
    if ($managedExecutables.Count -gt 0) {
        throw "VSIX unexpectedly contains platform-specific managed apphosts:`n$($managedExecutables -join "`n")"
    }

    Write-Host "VSIX audit passed: $($entries.Count) entries."
    Write-Host 'Required language server, execution host, grammar, configuration, and extension bundle payloads are present.'
}
finally {
    $archive.Dispose()
}
