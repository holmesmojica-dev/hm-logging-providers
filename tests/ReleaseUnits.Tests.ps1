[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\scripts\ReleaseUnits.psm1') -Force

function Assert-Equal { param($Expected, $Actual); if ($Expected -cne $Actual) { throw "Expected '$Expected', received '$Actual'." } }
function Assert-Throws { param([scriptblock]$Action); try { & $Action } catch { return }; throw 'Expected an exception.' }

$release = Resolve-HmProviderReleaseTag -Tag 'console-v1.2.3-preview.4'
Assert-Equal 'console' $release.ReleaseUnit
Assert-Equal '1.2.3-preview.4' $release.ReleaseVersion
Assert-Equal 'src/Hm.Logging.Providers.Console/Hm.Logging.Providers.Console.csproj' $release.Project
Assert-Equal 'HDev.Hm.Logging.Providers.Console' $release.PackageId
Assert-Equal $true $release.PublicationEnabled

foreach ($version in @('2.7.4-preview.9', '3.0.0', '42.1.0-rc.2')) {
    $authorizedRelease = Resolve-HmProviderReleaseTag -Tag "console-v$version" -RequirePublicationEnabled
    Assert-Equal "console-v$version" $authorizedRelease.Tag
    Assert-Equal $version $authorizedRelease.ReleaseVersion
    Assert-Equal 'console' $authorizedRelease.ReleaseUnit
    Assert-Equal 'HDev.Hm.Logging.Providers.Console' $authorizedRelease.PackageId
    Assert-Equal $true $authorizedRelease.PublicationEnabled
}

$files = Resolve-HmProviderReleaseTag -Tag 'files-v2.3.4-preview.5'
Assert-Equal 'files' $files.ReleaseUnit
Assert-Equal '2.3.4-preview.5' $files.ReleaseVersion
Assert-Equal 'src/Hm.Logging.Providers.Files/Hm.Logging.Providers.Files.csproj' $files.Project
Assert-Equal 'HDev.Hm.Logging.Providers.Files' $files.PackageId
Assert-Equal $false $files.PublicationEnabled
Assert-Throws { Resolve-HmProviderReleaseTag -Tag 'files-v2.3.4-preview.5' -RequirePublicationEnabled }

foreach ($tag in @('elasticsearch-v1.0.0', 'entityframework-v1.0.0', 'unknown-v1.0.0', 'console-v1.0', 'console-v01.0.0', 'console-v1.0.0-01', 'console-v1.0.0+build', 'Console-v1.0.0', 'v1.0.0')) {
    Assert-Throws { Resolve-HmProviderReleaseTag -Tag $tag }
}
Assert-Throws { Get-HmProviderReleaseUnit -Name 'Console' }
Write-Output 'Release-unit tests passed.'
