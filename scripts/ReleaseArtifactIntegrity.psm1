Set-StrictMode -Version Latest

function Get-HmProviderArtifactNames {
    param([Parameter(Mandatory)][string]$PackageId, [Parameter(Mandatory)][string]$ReleaseVersion)
    return @("$PackageId.$ReleaseVersion.nupkg", "$PackageId.$ReleaseVersion.snupkg")
}

function New-HmProviderArtifactManifest {
    param([Parameter(Mandatory)][string]$PackageDirectory, [Parameter(Mandatory)][string]$PackageId, [Parameter(Mandatory)][string]$ReleaseVersion)
    $directory = (Resolve-Path -LiteralPath $PackageDirectory).Path
    $lines = foreach ($name in Get-HmProviderArtifactNames -PackageId $PackageId -ReleaseVersion $ReleaseVersion) {
        $path = Join-Path $directory $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release artifact '$name' was not produced." }
        "$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()) *$name"
    }
    [IO.File]::WriteAllLines((Join-Path $directory 'release-artifacts.sha256'), $lines, [Text.UTF8Encoding]::new($false))
}

function Assert-HmProviderArtifactManifest {
    param([Parameter(Mandatory)][string]$PackageDirectory, [Parameter(Mandatory)][string]$PackageId, [Parameter(Mandatory)][string]$ReleaseVersion)
    $directory = (Resolve-Path -LiteralPath $PackageDirectory).Path
    $manifest = Join-Path $directory 'release-artifacts.sha256'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'The release artifact integrity manifest was not produced.' }
    $names = @(Get-HmProviderArtifactNames -PackageId $PackageId -ReleaseVersion $ReleaseVersion)
    $lines = @(Get-Content -LiteralPath $manifest)
    if ($lines.Count -ne $names.Count) { throw 'The release artifact integrity manifest has an unexpected number of entries.' }
    foreach ($name in $names) {
        $line = @($lines | Where-Object { $_ -match "^[0-9a-f]{64} \*$([regex]::Escape($name))$" })
        if ($line.Count -ne 1) { throw "The release artifact integrity manifest is missing '$name'." }
        $hash = (Get-FileHash -LiteralPath (Join-Path $directory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($line[0].Substring(0, 64) -cne $hash) { throw "Release artifact '$name' does not match its integrity manifest." }
    }
}

Export-ModuleMember -Function Get-HmProviderArtifactNames, New-HmProviderArtifactManifest, Assert-HmProviderArtifactManifest
