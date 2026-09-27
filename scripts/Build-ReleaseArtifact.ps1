[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseUnit,
    [Parameter(Mandatory)][string]$ReleaseVersion,
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseUnits.psm1') -Force
$root = [IO.Path]::GetFullPath((& git rev-parse --show-toplevel).Trim())
Assert-HmProviderCheckedOutCommit -RepositoryPath $root -SourceCommit $SourceCommit
$unit = Get-HmProviderReleaseUnit -Name $ReleaseUnit
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release output directory must be located inside the repository.'
}
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output | Out-Null

$properties = @("-p:ReleaseVersion=$ReleaseVersion", "-p:RepositoryCommit=$SourceCommit", "-p:SourceRevisionId=$SourceCommit", '-p:ContinuousIntegrationBuild=true')
& dotnet restore $unit.Project
if ($LASTEXITCODE -ne 0) { throw 'Release package restore failed.' }
& dotnet build $unit.Project --configuration Release --no-restore @properties
if ($LASTEXITCODE -ne 0) { throw 'Release package build failed.' }
& dotnet pack $unit.Project --configuration Release --no-build --no-restore --output $output @properties
if ($LASTEXITCODE -ne 0) { throw 'Release packaging failed.' }

& (Join-Path $PSScriptRoot 'Validate-ReleaseArtifact.ps1') -PackageDirectory $output -ReleaseUnit $ReleaseUnit -ReleaseVersion $ReleaseVersion -SourceCommit $SourceCommit
& (Join-Path $PSScriptRoot 'New-ReleaseArtifactManifest.ps1') -PackageDirectory $output -PackageId $unit.PackageId -ReleaseVersion $ReleaseVersion
