[CmdletBinding()]
param([switch]$CollectCoverage)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-ValidationCommand {
    param([Parameter(Mandatory)][string]$Command, [string[]]$Arguments = @())
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) { throw "Required command '$Command' was not found on PATH." }
    Write-Output "==> $Command $($Arguments -join ' ')"
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Validation command '$Command' failed with exit code $LASTEXITCODE." }
}

$root = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) { throw 'Validation must run within the repository.' }
Push-Location $root
try {
    Invoke-ValidationCommand dotnet @('restore', 'Hm.Logging.Providers.slnx')
    Invoke-ValidationCommand dotnet @('format', 'Hm.Logging.Providers.slnx', '--no-restore', '--verify-no-changes')
    Invoke-ValidationCommand dotnet @('build', 'Hm.Logging.Providers.slnx', '--configuration', 'Release', '--no-restore')
    $test = @('test', 'Hm.Logging.Providers.slnx', '--configuration', 'Release', '--no-build', '--no-restore')
    if ($CollectCoverage) {
        $coverage = Join-Path $root 'TestResults/coverage'
        New-Item -ItemType Directory -Path $coverage -Force | Out-Null
        Get-ChildItem -LiteralPath $coverage -Filter '*.xml' -File | Remove-Item -Force
        $test += @('--coverage', '--coverage-output-format', 'xml', '--results-directory', $coverage, '--coverage-settings', (Join-Path $root 'scripts/code-coverage.settings.xml'))
    }
    Invoke-ValidationCommand dotnet $test
    foreach ($script in @('tests/ReleaseUnits.Tests.ps1', 'tests/NuGetPublicationPreflight.Tests.ps1', 'tests/ReleaseInfrastructure.Tests.ps1')) {
        Invoke-ValidationCommand pwsh @('-NoProfile', '-File', $script)
    }
}
finally { Pop-Location }
