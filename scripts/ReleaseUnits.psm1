Set-StrictMode -Version Latest

function Get-HmProviderReleaseUnit {
    param([Parameter(Mandatory)][string]$Name)

    if ($Name -cne $Name.ToLowerInvariant()) {
        throw "Provider release unit '$Name' must use its lowercase canonical identity."
    }

    $units = @{
        console = [pscustomobject]@{
            Name = 'console'
            Project = 'src/Hm.Logging.Providers.Console/Hm.Logging.Providers.Console.csproj'
            PackageId = 'HDev.Hm.Logging.Providers.Console'
            AssemblyName = 'Hm.Logging.Providers.Console'
            PublicationEnabled = $true
        }
    }

    if (-not $units.ContainsKey($Name)) {
        throw "Unknown or unsupported provider release unit '$Name'."
    }

    return $units[$Name]
}

function Test-HmCanonicalNonNegativeInteger {
    param([Parameter(Mandatory)][string]$Value)
    return $Value -eq '0' -or $Value -match '^[1-9][0-9]*$'
}

function Assert-HmProviderReleaseVersion {
    param([Parameter(Mandatory)][string]$ReleaseVersion)

    if ($ReleaseVersion.Contains('+', [System.StringComparison]::Ordinal)) {
        throw "Release version '$ReleaseVersion' must not contain SemVer build metadata."
    }

    $parts = $ReleaseVersion.Split('-', 2)
    $core = $parts[0].Split('.')
    if ($core.Count -ne 3 -or @($core | Where-Object { -not (Test-HmCanonicalNonNegativeInteger $_) }).Count -ne 0) {
        throw "Release version '$ReleaseVersion' must contain a canonical major.minor.patch version."
    }

    if ($parts.Count -ne 2) { return }

    if ([string]::IsNullOrWhiteSpace($parts[1])) { throw "Release version '$ReleaseVersion' has an empty prerelease identifier." }
    foreach ($identifier in $parts[1].Split('.')) {
        if ([string]::IsNullOrEmpty($identifier) -or $identifier -notmatch '^[0-9A-Za-z-]+$') {
            throw "Release version '$ReleaseVersion' has an invalid prerelease identifier."
        }
        if ($identifier -match '^[0-9]+$' -and -not (Test-HmCanonicalNonNegativeInteger $identifier)) {
            throw "Release version '$ReleaseVersion' has a prerelease numeric identifier with a leading zero."
        }
    }
}

function Resolve-HmProviderReleaseTag {
    param(
        [Parameter(Mandatory)][string]$Tag,
        [switch]$RequirePublicationEnabled
    )

    if ($Tag -notmatch '^([a-z][a-z0-9-]*)-v(.+)$') {
        throw "Release tag '$Tag' must use the canonical <provider>-v<SemVer> form."
    }

    $releaseUnit = $Matches[1]
    if ($releaseUnit -cne $releaseUnit.ToLowerInvariant()) {
        throw "Release unit '$releaseUnit' must use its lowercase canonical identity."
    }
    $unit = Get-HmProviderReleaseUnit -Name $releaseUnit
    $version = $Matches[2]
    Assert-HmProviderReleaseVersion -ReleaseVersion $version
    if ($RequirePublicationEnabled -and -not $unit.PublicationEnabled) {
        throw "Provider release unit '$($unit.Name)' is recognized but not enabled for publication."
    }

    return [pscustomobject]@{
        Tag = $Tag
        ReleaseVersion = $version
        ReleaseUnit = $unit.Name
        Project = $unit.Project
        PackageId = $unit.PackageId
        AssemblyName = $unit.AssemblyName
        PublicationEnabled = $unit.PublicationEnabled
    }
}

function Resolve-HmProviderReleaseContext {
    param(
        [Parameter(Mandatory)][string]$RepositoryPath,
        [Parameter(Mandatory)][string]$Tag,
        [Parameter(Mandatory)][string]$MainBranch,
        [switch]$RequirePublicationEnabled
    )

    $release = Resolve-HmProviderReleaseTag -Tag $Tag -RequirePublicationEnabled:$RequirePublicationEnabled
    $commit = (& git -C $RepositoryPath rev-parse --verify "$Tag^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) { throw "Release tag '$Tag' does not resolve to a commit." }
    & git -C $RepositoryPath rev-parse --verify "$MainBranch^{commit}" *> $null
    if ($LASTEXITCODE -ne 0) { throw "Main branch reference '$MainBranch' does not resolve to a commit." }
    & git -C $RepositoryPath merge-base --is-ancestor $commit $MainBranch
    if ($LASTEXITCODE -ne 0) { throw "Tagged commit '$commit' is not in the history of '$MainBranch'." }

    $release | Add-Member -NotePropertyName SourceCommit -NotePropertyValue $commit
    return $release
}

function Assert-HmProviderCheckedOutCommit {
    param([Parameter(Mandatory)][string]$RepositoryPath, [Parameter(Mandatory)][string]$SourceCommit)
    $actual = (& git -C $RepositoryPath rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $actual -cne $SourceCommit) {
        throw "Checked-out commit '$actual' does not match release source commit '$SourceCommit'."
    }
}

Export-ModuleMember -Function Get-HmProviderReleaseUnit, Assert-HmProviderReleaseVersion, Resolve-HmProviderReleaseTag, Resolve-HmProviderReleaseContext, Assert-HmProviderCheckedOutCommit
