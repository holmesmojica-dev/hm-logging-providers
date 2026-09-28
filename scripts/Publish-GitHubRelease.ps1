[CmdletBinding()]
param([Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string]$ReleaseVersion, [Parameter(Mandatory)][string]$Repository)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseUnits.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubRelease.psm1') -Force
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "Required command 'gh' was not found on PATH." }
$release = Resolve-HmProviderReleaseTag -Tag $Tag -RequirePublicationEnabled
if ($release.ReleaseVersion -cne $ReleaseVersion) { throw 'GitHub Release identity does not match the resolved release version.' }
$prerelease = $ReleaseVersion.Contains('-', [StringComparison]::Ordinal)
$output = @(& gh release view $Tag --repo $Repository --json tagName,isPrerelease 2>&1 | ForEach-Object ToString)
$lookup = Resolve-HmProviderGitHubReleaseLookup -Tag $Tag -ExpectedPrerelease $prerelease -ExitCode $LASTEXITCODE -Output $output
if ($lookup.State -eq 'existing') { Write-Output "GitHub Release '$Tag' is already verified."; return }
& gh @(Get-HmProviderGitHubReleaseCreateArguments -Tag $Tag -Repository $Repository -Prerelease $prerelease)
if ($LASTEXITCODE -ne 0) { throw "GitHub Release creation failed for '$Tag'." }
