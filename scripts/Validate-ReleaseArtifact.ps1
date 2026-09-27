[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$ReleaseUnit,
    [Parameter(Mandatory)][string]$ReleaseVersion,
    [Parameter(Mandatory)][string]$SourceCommit,
    [switch]$SkipSymbolPackage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Import-Module (Join-Path $PSScriptRoot 'ReleaseUnits.psm1') -Force

$unit = Get-HmProviderReleaseUnit -Name $ReleaseUnit
$directory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packagePath = Join-Path $directory "$($unit.PackageId).$ReleaseVersion.nupkg"
$symbolPath = Join-Path $directory "$($unit.PackageId).$ReleaseVersion.snupkg"
if (-not (Test-Path -LiteralPath $packagePath) -or (-not $SkipSymbolPackage -and -not (Test-Path -LiteralPath $symbolPath))) {
    throw 'The expected .nupkg and .snupkg release artifacts were not produced.'
}

$package = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $required = @('README.md', 'LICENSE', 'icon.png', "lib/net10.0/$($unit.AssemblyName).dll", "lib/net10.0/$($unit.AssemblyName).xml")
    foreach ($entry in $required) {
        if ($package.Entries.FullName -cnotcontains $entry) { throw "Release package is missing '$entry'." }
    }
    $nuspecEntry = @($package.Entries | Where-Object FullName -ceq "$($unit.PackageId).nuspec")
    if ($nuspecEntry.Count -ne 1) { throw 'Release package must contain exactly one expected nuspec.' }
    $reader = [IO.StreamReader]::new($nuspecEntry[0].Open())
    try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $nuspec.package.metadata
    if ($metadata.id -cne $unit.PackageId -or $metadata.version -cne $ReleaseVersion -or $metadata.repository.commit -cne $SourceCommit) {
        throw 'Release package metadata does not match the package and source identity.'
    }
    if ($metadata.icon -cne 'icon.png' -or $metadata.readme -cne 'README.md' -or $metadata.license.type -cne 'expression' -or $metadata.license.'#text' -cne 'MIT') {
        throw 'Release package metadata is missing the required icon, README, or MIT license expression.'
    }
    $dependencies = @($metadata.dependencies.group.dependency)
    $core = @($dependencies | Where-Object id -ceq 'HDev.Hm.Logging.Core')
    if ($core.Count -ne 1 -or $core[0].version -cne '0.1.0-preview.9') {
        throw 'Release package must declare the approved HDev.Hm.Logging.Core dependency baseline.'
    }
    $icon = $package.GetEntry('icon.png')
    $stream = $icon.Open()
    try {
        $signature = [byte[]]::new(8)
        if ($stream.Read($signature, 0, 8) -ne 8 -or [Convert]::ToHexString($signature) -cne '89504E470D0A1A0A') { throw 'Packaged icon does not have a valid PNG signature.' }
    } finally { $stream.Dispose() }
}
finally { $package.Dispose() }

if (-not $SkipSymbolPackage) {
    $symbols = [IO.Compression.ZipFile]::OpenRead($symbolPath)
    try {
        $pdb = @($symbols.Entries | Where-Object FullName -ceq "lib/net10.0/$($unit.AssemblyName).pdb")
        if ($pdb.Count -ne 1) { throw 'Symbol package does not contain the expected portable PDB.' }
        $stream = $pdb[0].Open()
        try {
            $memory = [IO.MemoryStream]::new()
            try {
                $stream.CopyTo($memory); $memory.Position = 0
                $provider = [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($memory)
                try {
                    $reader = $provider.GetMetadataReader()
                    $sourceLinkGuid = [Guid]'CC110556-A091-4D38-9FEC-25AB9A351A6A'
                    $sourceLink = @($reader.CustomDebugInformation | Where-Object { $reader.GetGuid($reader.GetCustomDebugInformation($_).Kind) -eq $sourceLinkGuid })
                    if ($sourceLink.Count -ne 1) { throw 'Portable PDB does not contain Source Link metadata.' }
                } finally { $provider.Dispose() }
            } finally { $memory.Dispose() }
        } finally { $stream.Dispose() }
    } finally { $symbols.Dispose() }
}
