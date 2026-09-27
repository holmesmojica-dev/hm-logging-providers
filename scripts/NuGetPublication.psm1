Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseUnits.psm1') -Force

function Assert-HmProviderNuGetPublicationPrerequisites {
    param(
        [Parameter(Mandatory)][string]$PackageDirectory,
        [Parameter(Mandatory)][string]$ReleaseUnit,
        [Parameter(Mandatory)][string]$ReleaseVersion,
        [Parameter(Mandatory)][string]$SourceCommit
    )

    $unit = Get-HmProviderReleaseUnit -Name $ReleaseUnit
    if (-not $unit.PublicationEnabled) {
        throw "Provider release unit '$ReleaseUnit' is not enabled for publication."
    }

    & (Join-Path $PSScriptRoot 'Validate-ReleaseArtifact.ps1') -PackageDirectory $PackageDirectory -ReleaseUnit $ReleaseUnit -ReleaseVersion $ReleaseVersion -SourceCommit $SourceCommit
    & (Join-Path $PSScriptRoot 'Assert-ReleaseArtifactManifest.ps1') -PackageDirectory $PackageDirectory -PackageId $unit.PackageId -ReleaseVersion $ReleaseVersion

    return $unit
}

function Get-HmProviderNuGetPackageName {
    param([Parameter(Mandatory)][string]$PackageId, [Parameter(Mandatory)][string]$ReleaseVersion)
    return "$PackageId.$ReleaseVersion.nupkg"
}

function Get-HmProviderNuGetPackageUri {
    param([Parameter(Mandatory)][string]$PackageId, [Parameter(Mandatory)][string]$ReleaseVersion)
    $name = (Get-HmProviderNuGetPackageName -PackageId $PackageId -ReleaseVersion $ReleaseVersion).ToLowerInvariant()
    return "https://api.nuget.org/v3-flatcontainer/$($PackageId.ToLowerInvariant())/$($ReleaseVersion.ToLowerInvariant())/$name"
}

function Resolve-HmProviderNuGetDecision {
    param([Parameter(Mandatory)][bool]$PackageExists, [Parameter(Mandatory)][bool]$IdentityMatches)
    if (-not $PackageExists) { return 'publish' }
    if ($IdentityMatches) { return 'already_verified' }
    throw 'The existing NuGet package has a conflicting release identity.'
}

function Test-HmNuGetNotFoundStatusCode { param([object]$StatusCode); return $null -ne $StatusCode -and [int]$StatusCode -eq 404 }

Export-ModuleMember -Function Assert-HmProviderNuGetPublicationPrerequisites, Get-HmProviderNuGetPackageName, Get-HmProviderNuGetPackageUri, Resolve-HmProviderNuGetDecision, Test-HmNuGetNotFoundStatusCode
