[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter()][string]$MainBranch = 'main',
    [Parameter()][string]$GitHubOutputPath,
    [switch]$RequirePublicationEnabled
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseUnits.psm1') -Force

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
$release = Resolve-HmProviderReleaseContext -RepositoryPath $repositoryRoot -Tag $Tag -MainBranch $MainBranch -RequirePublicationEnabled:$RequirePublicationEnabled

if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    @(
        "release_unit=$($release.ReleaseUnit)"
        "release_version=$($release.ReleaseVersion)"
        "release_tag=$($release.Tag)"
        "source_commit=$($release.SourceCommit)"
        "project=$($release.Project)"
        "package_id=$($release.PackageId)"
        "assembly_name=$($release.AssemblyName)"
    ) | Add-Content -LiteralPath $GitHubOutputPath
}

$release | ConvertTo-Json -Compress
