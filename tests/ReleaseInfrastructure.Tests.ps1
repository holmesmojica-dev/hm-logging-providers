[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\scripts\NuGetPublication.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\scripts\GitHubRelease.psm1') -Force

function Assert-Equal { param($Expected, $Actual); if ($Expected -cne $Actual) { throw "Expected '$Expected', received '$Actual'." } }
function Assert-Throws { param([scriptblock]$Action); try { & $Action } catch { return }; throw 'Expected an exception.' }

Assert-Equal 'publish' (Resolve-HmProviderNuGetDecision -PackageExists $false -IdentityMatches $false)
Assert-Equal 'already_verified' (Resolve-HmProviderNuGetDecision -PackageExists $true -IdentityMatches $true)
Assert-Throws { Resolve-HmProviderNuGetDecision -PackageExists $true -IdentityMatches $false }
Assert-Equal $true (Test-HmNuGetNotFoundStatusCode 404)
Assert-Equal $false (Test-HmNuGetNotFoundStatusCode 401)

foreach ($tag in @('console-v1.2.3-preview.4', 'files-v2.3.4-preview.5')) {
    $existingOutput = "{`"tagName`":`"$tag`",`"isPrerelease`":true}"
    $existing = Resolve-HmProviderGitHubReleaseLookup -Tag $tag -ExpectedPrerelease $true -ExitCode 0 -Output @($existingOutput)
    Assert-Equal 'existing' $existing.State
    Assert-Equal 'absent' (Resolve-HmProviderGitHubReleaseLookup -Tag $tag -ExpectedPrerelease $true -ExitCode 1 -Output @('release not found')).State
    Assert-Throws { Resolve-HmProviderGitHubReleaseLookup -Tag $tag -ExpectedPrerelease $true -ExitCode 1 -Output @('authentication failed') }
    Assert-Throws { Resolve-HmProviderGitHubReleaseLookup -Tag $tag -ExpectedPrerelease $true -ExitCode 0 -Output @('{"tagName":"unexpected-v1.0.0","isPrerelease":true}') }
    $arguments = Get-HmProviderGitHubReleaseCreateArguments -Tag $tag -Repository 'owner/repository' -Prerelease $true
    Assert-Equal $true ($arguments -contains '--verify-tag')
    Assert-Equal $true ($arguments -contains '--generate-notes')
    Assert-Equal $true ($arguments -contains '--prerelease')
}

$root = (& git rev-parse --show-toplevel).Trim()
$workflow = Get-Content -LiteralPath (Join-Path $root '.github/workflows/delivery.yml') -Raw
if ($workflow -notmatch '(?ms)^permissions:\s+contents: read' -or $workflow -notmatch '(?ms)^  publish-nuget:.*?permissions:.*?id-token: write') { throw 'Delivery permissions are not fail-closed and job-scoped.' }
$jobMatches = [regex]::Matches($workflow, '(?ms)^  (?<name>[a-z][a-z0-9-]*):\r?\n(?<body>.*?)(?=^  [a-z][a-z0-9-]*:\r?\n|\z)')
foreach ($job in $jobMatches) {
    if ($job.Groups['name'].Value -cne 'publish-nuget' -and $job.Groups['body'].Value -match 'id-token:\s*write') {
        throw "OIDC permission is granted to non-publication job '$($job.Groups['name'].Value)'."
    }
}
if ($workflow -match 'HDev\.Hm\.Logging\.Providers\.(Console|Files)|(?:console|files)-v') {
    throw 'Delivery must not hardcode a provider package or tag identity.'
}
$releaseUnitParameter = '-ReleaseUnit "${{ needs.preflight.outputs.release_unit }}"'
Assert-Equal 3 ([regex]::Matches($workflow, [regex]::Escape($releaseUnitParameter)).Count)
$genericFlow = @(
    'Assert-EligibleMainCommit.ps1',
    'Build-ReleaseArtifact.ps1',
    'Assert-ReleaseArtifactManifest.ps1',
    'Resolve-NuGetRelease.ps1',
    'actions/attest@',
    'Publish-NuGetRelease.ps1',
    'Publish-GitHubRelease.ps1'
)
$previousIndex = -1
foreach ($component in $genericFlow) {
    $index = $workflow.IndexOf($component, [StringComparison]::Ordinal)
    if ($index -le $previousIndex) { throw "Generic Delivery component '$component' is missing or out of order." }
    $previousIndex = $index
}
Write-Output 'Release infrastructure tests passed.'
