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
Import-Module (Join-Path $PSScriptRoot 'NuGetPublication.psm1') -Force

$unit = Assert-HmProviderNuGetPublicationPrerequisites -PackageDirectory $PackageDirectory -ReleaseUnit $ReleaseUnit -ReleaseVersion $ReleaseVersion -SourceCommit $SourceCommit
if ([string]::IsNullOrWhiteSpace($env:NUGET_TRUSTED_PUBLISHING_API_KEY)) { throw 'NUGET_TRUSTED_PUBLISHING_API_KEY must be supplied.' }
$package = Join-Path $PackageDirectory (Get-HmProviderNuGetPackageName -PackageId $unit.PackageId -ReleaseVersion $ReleaseVersion)
& dotnet nuget push $package --api-key $env:NUGET_TRUSTED_PUBLISHING_API_KEY --source 'https://api.nuget.org/v3/index.json'
if ($LASTEXITCODE -ne 0) { throw 'NuGet publication failed.' }

$temporary = Join-Path ([IO.Path]::GetTempPath()) "hm-provider-published-$([Guid]::NewGuid())"
try {
    New-Item -ItemType Directory -Path $temporary | Out-Null
    $remote = Join-Path $temporary ([IO.Path]::GetFileName($package))
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        try { Invoke-WebRequest -Uri (Get-HmProviderNuGetPackageUri -PackageId $unit.PackageId -ReleaseVersion $ReleaseVersion) -OutFile $remote -TimeoutSec 30; break }
        catch {
            if ($attempt -eq 60) { throw 'NuGet did not make the package available for verification within 15 minutes.' }
            Start-Sleep -Seconds 15
        }
    }
    & (Join-Path $PSScriptRoot 'Validate-ReleaseArtifact.ps1') -PackageDirectory $temporary -ReleaseUnit $ReleaseUnit -ReleaseVersion $ReleaseVersion -SourceCommit $SourceCommit -SkipSymbolPackage
    Assert-HmProviderNuGetContentIdentity -LocalPackagePath $package -RemotePackagePath $remote
    'nuget_state=published' | Add-Content -LiteralPath $GitHubOutputPath
}
finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force } }
