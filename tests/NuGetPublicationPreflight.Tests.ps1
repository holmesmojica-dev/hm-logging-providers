[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

function Assert-Equal {
    param($Expected, $Actual)
    if ($Expected -cne $Actual) { throw "Expected '$Expected', received '$Actual'." }
}

function Assert-ThrowsLike {
    param([scriptblock]$Action, [string]$Pattern)
    try { & $Action }
    catch {
        if ($_.Exception.Message -notlike $Pattern) {
            throw "Expected failure matching '$Pattern', received '$($_.Exception.Message)'."
        }
        return
    }
    throw "Expected failure matching '$Pattern'."
}

function Add-ZipEntry {
    param($Archive, [string]$Name, [byte[]]$Bytes)
    $stream = $Archive.CreateEntry($Name).Open()
    try { $stream.Write($Bytes, 0, $Bytes.Length) }
    finally { $stream.Dispose() }
}

function Write-ArtifactManifest {
    param([string]$Directory, [string]$PackageId, [string]$Version, [switch]$InvalidatePackage)
    $packageName = "$PackageId.$Version.nupkg"
    $symbolName = "$PackageId.$Version.snupkg"
    $packageHash = if ($InvalidatePackage) { '0' * 64 } else { (Get-FileHash -LiteralPath (Join-Path $Directory $packageName) -Algorithm SHA256).Hash.ToLowerInvariant() }
    $symbolHash = (Get-FileHash -LiteralPath (Join-Path $Directory $symbolName) -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllLines(
        (Join-Path $Directory 'release-artifacts.sha256'),
        @("$packageHash *$packageName", "$symbolHash *$symbolName"),
        [Text.UTF8Encoding]::new($false))
}

$root = (& git rev-parse --show-toplevel).Trim()
$scripts = Join-Path $root 'scripts'
$temporary = Join-Path ([IO.Path]::GetTempPath()) "hm-provider-preflight-$([Guid]::NewGuid().ToString('N'))"
$isolatedScripts = Join-Path $temporary 'scripts'
$packageDirectory = Join-Path $temporary 'packages'
$packageId = 'HDev.Hm.Logging.Providers.Console'
$assemblyName = 'Hm.Logging.Providers.Console'
$releaseVersion = '1.2.3-preview.4'
$sourceCommit = '1234567890123456789012345678901234567890'

New-Item -ItemType Directory -Path $isolatedScripts, $packageDirectory | Out-Null
try {
    Import-Module (Join-Path $scripts 'NuGetPublication.psm1') -Force
    Assert-ThrowsLike {
        Assert-HmProviderNuGetPublicationPrerequisites -PackageDirectory $packageDirectory -ReleaseUnit unknown -ReleaseVersion $releaseVersion -SourceCommit $sourceCommit
    } "*Unknown or unsupported provider release unit 'unknown'.*"

    Remove-Module NuGetPublication, ReleaseUnits -ErrorAction SilentlyContinue
    foreach ($name in @('NuGetPublication.psm1', 'Validate-ReleaseArtifact.ps1', 'Assert-ReleaseArtifactManifest.ps1', 'ReleaseArtifactIntegrity.psm1')) {
        Copy-Item -LiteralPath (Join-Path $scripts $name) -Destination (Join-Path $isolatedScripts $name)
    }
    $releaseUnits = Get-Content -LiteralPath (Join-Path $scripts 'ReleaseUnits.psm1') -Raw
    $disabledReleaseUnits = $releaseUnits.Replace('PublicationEnabled = $true', 'PublicationEnabled = $false')
    [IO.File]::WriteAllText((Join-Path $isolatedScripts 'ReleaseUnits.psm1'), $disabledReleaseUnits, [Text.UTF8Encoding]::new($false))
    Import-Module (Join-Path $isolatedScripts 'NuGetPublication.psm1') -Force
    Assert-ThrowsLike {
        Assert-HmProviderNuGetPublicationPrerequisites -PackageDirectory $packageDirectory -ReleaseUnit console -ReleaseVersion $releaseVersion -SourceCommit $sourceCommit
    } "*not enabled for publication*"
    Remove-Module NuGetPublication, ReleaseUnits -ErrorAction SilentlyContinue

    [IO.File]::WriteAllText((Join-Path $isolatedScripts 'ReleaseUnits.psm1'), $releaseUnits, [Text.UTF8Encoding]::new($false))

    $packagePath = Join-Path $packageDirectory "$packageId.$releaseVersion.nupkg"
    $package = [IO.Compression.ZipFile]::Open($packagePath, 'Create')
    try {
        $nuspec = "<package><metadata><id>$packageId</id><version>$releaseVersion</version><license type='expression'>MIT</license><icon>icon.png</icon><readme>README.md</readme><repository commit='$sourceCommit'/><dependencies><group targetFramework='net10.0'><dependency id='HDev.Hm.Logging.Core' version='1.0.0-preview.1'/></group></dependencies></metadata></package>"
        Add-ZipEntry $package "$packageId.nuspec" ([Text.Encoding]::UTF8.GetBytes($nuspec))
        Add-ZipEntry $package 'README.md' ([Text.Encoding]::UTF8.GetBytes('readme'))
        Add-ZipEntry $package 'LICENSE' ([Text.Encoding]::UTF8.GetBytes('license'))
        Add-ZipEntry $package 'icon.png' ([IO.File]::ReadAllBytes((Join-Path $root 'icon.png')))
        Add-ZipEntry $package "lib/net10.0/$assemblyName.dll" ([byte[]]@(1))
        Add-ZipEntry $package "lib/net10.0/$assemblyName.xml" ([byte[]]@(1))
    }
    finally { $package.Dispose() }

    $pdbPath = Join-Path $root "src/$assemblyName/bin/Release/net10.0/$assemblyName.pdb"
    if (-not (Test-Path -LiteralPath $pdbPath -PathType Leaf)) { throw 'Release build output is required for preflight tests.' }
    $symbolPath = Join-Path $packageDirectory "$packageId.$releaseVersion.snupkg"
    $symbols = [IO.Compression.ZipFile]::Open($symbolPath, 'Create')
    try { Add-ZipEntry $symbols "lib/net10.0/$assemblyName.pdb" ([IO.File]::ReadAllBytes($pdbPath)) }
    finally { $symbols.Dispose() }
    Write-ArtifactManifest -Directory $packageDirectory -PackageId $packageId -Version $releaseVersion

    Import-Module (Join-Path $isolatedScripts 'NuGetPublication.psm1') -Force
    Assert-ThrowsLike {
        Assert-HmProviderNuGetPublicationPrerequisites -PackageDirectory $packageDirectory -ReleaseUnit console -ReleaseVersion $releaseVersion -SourceCommit ('f' * 40)
    } '*metadata does not match*'

    Write-ArtifactManifest -Directory $packageDirectory -PackageId $packageId -Version $releaseVersion -InvalidatePackage
    Assert-ThrowsLike {
        Assert-HmProviderNuGetPublicationPrerequisites -PackageDirectory $packageDirectory -ReleaseUnit console -ReleaseVersion $releaseVersion -SourceCommit $sourceCommit
    } '*does not match its integrity manifest*'

    Write-ArtifactManifest -Directory $packageDirectory -PackageId $packageId -Version $releaseVersion
    $unit = Assert-HmProviderNuGetPublicationPrerequisites -PackageDirectory $packageDirectory -ReleaseUnit console -ReleaseVersion $releaseVersion -SourceCommit $sourceCommit
    Assert-Equal 'console' $unit.Name
    Assert-Equal $packageId $unit.PackageId
    Assert-Equal $true $unit.PublicationEnabled

    $resolveScript = Get-Content -LiteralPath (Join-Path $scripts 'Resolve-NuGetRelease.ps1') -Raw
    $publishScript = Get-Content -LiteralPath (Join-Path $scripts 'Publish-NuGetRelease.ps1') -Raw
    $preflightName = 'Assert-HmProviderNuGetPublicationPrerequisites'
    $contentIdentityName = 'Assert-HmProviderNuGetContentIdentity'
    Assert-Equal 1 ([regex]::Matches($resolveScript, $preflightName).Count)
    Assert-Equal 1 ([regex]::Matches($publishScript, $preflightName).Count)
    Assert-Equal 1 ([regex]::Matches($resolveScript, $contentIdentityName).Count)
    Assert-Equal 1 ([regex]::Matches($publishScript, $contentIdentityName).Count)
    if ($resolveScript.IndexOf($preflightName, [StringComparison]::Ordinal) -gt $resolveScript.IndexOf('Invoke-WebRequest', [StringComparison]::Ordinal)) {
        throw 'Resolve must execute the shared preflight before remote resolution.'
    }
    if ($publishScript.IndexOf($preflightName, [StringComparison]::Ordinal) -gt $publishScript.IndexOf('NUGET_TRUSTED_PUBLISHING_API_KEY', [StringComparison]::Ordinal)) {
        throw 'Publish must execute the shared preflight before credentialed publication.'
    }
    if ($resolveScript.IndexOf('Validate-ReleaseArtifact.ps1', [StringComparison]::Ordinal) -gt $resolveScript.IndexOf($contentIdentityName, [StringComparison]::Ordinal) -or
        $resolveScript.IndexOf($contentIdentityName, [StringComparison]::Ordinal) -gt $resolveScript.IndexOf('Resolve-HmProviderNuGetDecision', [StringComparison]::Ordinal)) {
        throw 'Resolve must validate remote structure and content identity before deciding the NuGet state.'
    }
    if ($publishScript.LastIndexOf('Validate-ReleaseArtifact.ps1', [StringComparison]::Ordinal) -gt $publishScript.IndexOf($contentIdentityName, [StringComparison]::Ordinal) -or
        $publishScript.IndexOf($contentIdentityName, [StringComparison]::Ordinal) -gt $publishScript.IndexOf('nuget_state=published', [StringComparison]::Ordinal)) {
        throw 'Publish must validate remote structure and content identity before reporting publication.'
    }
}
finally {
    Remove-Module NuGetPublication, ReleaseUnits -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}

Write-Output 'NuGet publication preflight tests passed.'
