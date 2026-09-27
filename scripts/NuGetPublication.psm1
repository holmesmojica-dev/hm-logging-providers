Set-StrictMode -Version Latest

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

Export-ModuleMember -Function Get-HmProviderNuGetPackageName, Get-HmProviderNuGetPackageUri, Resolve-HmProviderNuGetDecision, Test-HmNuGetNotFoundStatusCode
