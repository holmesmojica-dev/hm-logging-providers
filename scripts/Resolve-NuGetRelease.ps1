[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$ReleaseUnit,
    [Parameter(Mandatory)][string]$ReleaseVersion,
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][string]$GitHubOutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseUnits.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'NuGetPublication.psm1') -Force
$unit = Get-HmProviderReleaseUnit -Name $ReleaseUnit
if (-not $unit.PublicationEnabled) { throw "Provider release unit '$ReleaseUnit' is not enabled for publication." }
& (Join-Path $PSScriptRoot 'Validate-ReleaseArtifact.ps1') -PackageDirectory $PackageDirectory -ReleaseUnit $ReleaseUnit -ReleaseVersion $ReleaseVersion -SourceCommit $SourceCommit
& (Join-Path $PSScriptRoot 'Assert-ReleaseArtifactManifest.ps1') -PackageDirectory $PackageDirectory -PackageId $unit.PackageId -ReleaseVersion $ReleaseVersion

$temporary = Join-Path ([IO.Path]::GetTempPath()) "hm-provider-nuget-$([Guid]::NewGuid())"
try {
    New-Item -ItemType Directory -Path $temporary | Out-Null
    $remote = Join-Path $temporary (Get-HmProviderNuGetPackageName -PackageId $unit.PackageId -ReleaseVersion $ReleaseVersion)
    $exists = $false
    try { Invoke-WebRequest -Uri (Get-HmProviderNuGetPackageUri -PackageId $unit.PackageId -ReleaseVersion $ReleaseVersion) -OutFile $remote -TimeoutSec 30; $exists = $true }
    catch {
        $status = if ($null -ne $_.Exception.Response) { $_.Exception.Response.StatusCode } else { $null }
        if (-not (Test-HmNuGetNotFoundStatusCode $status)) { throw }
    }
    $matches = $false
    if ($exists) {
        try { & (Join-Path $PSScriptRoot 'Validate-ReleaseArtifact.ps1') -PackageDirectory $temporary -ReleaseUnit $ReleaseUnit -ReleaseVersion $ReleaseVersion -SourceCommit $SourceCommit -SkipSymbolPackage; $matches = $true } catch { $matches = $false }
    }
    $decision = Resolve-HmProviderNuGetDecision -PackageExists $exists -IdentityMatches $matches
    @("nuget_state=$decision") | Add-Content -LiteralPath $GitHubOutputPath
}
finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force } }
