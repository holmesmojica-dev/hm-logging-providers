Set-StrictMode -Version Latest

function Resolve-HmProviderGitHubReleaseLookup {
    param([Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][bool]$ExpectedPrerelease, [Parameter(Mandatory)][int]$ExitCode, [Parameter(Mandatory)][string[]]$Output)
    $text = ($Output -join [Environment]::NewLine).Trim()
    if ($ExitCode -ne 0) {
        if ($text -ceq 'release not found') { return [pscustomobject]@{ State = 'absent' } }
        throw "GitHub Release lookup for '$Tag' failed: $text"
    }
    try { $release = $text | ConvertFrom-Json } catch { throw "GitHub Release lookup for '$Tag' returned malformed JSON." }
    if ($release.tagName -cne $Tag -or [bool]$release.isPrerelease -ne $ExpectedPrerelease) { throw "GitHub Release '$Tag' has a conflicting identity." }
    return [pscustomobject]@{ State = 'existing' }
}

function Get-HmProviderGitHubReleaseCreateArguments {
    param([Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string]$Repository, [Parameter(Mandatory)][bool]$Prerelease)
    $arguments = @('release', 'create', $Tag, '--repo', $Repository, '--title', $Tag, '--generate-notes', '--verify-tag')
    if ($Prerelease) { $arguments += '--prerelease' }
    return $arguments
}

Export-ModuleMember -Function Resolve-HmProviderGitHubReleaseLookup, Get-HmProviderGitHubReleaseCreateArguments
