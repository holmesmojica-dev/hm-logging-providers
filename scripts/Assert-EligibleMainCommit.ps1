[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$Repository
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "Required command 'gh' was not found on PATH." }

$json = & gh api --method GET "repos/$Repository/actions/workflows/quality.yml/runs" -f "head_sha=$Commit" -f branch=main -f event=push -f status=completed -f per_page=100
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($json | Out-String))) {
    throw "Unable to determine Quality eligibility for main commit '$Commit'."
}

try { $runs = (($json | Out-String) | ConvertFrom-Json).workflow_runs } catch { throw 'GitHub returned malformed Quality workflow evidence.' }
$eligible = @($runs | Where-Object {
    $_.head_sha -ceq $Commit -and $_.head_branch -ceq 'main' -and $_.event -ceq 'push' -and
    $_.status -ceq 'completed' -and $_.conclusion -ceq 'success' -and $_.path -ceq '.github/workflows/quality.yml'
})
if ($eligible.Count -lt 1) { throw "Commit '$Commit' has no successful completed main push Quality run and is not Delivery-eligible." }

Write-Output "Main Quality eligibility verified for '$Commit'."
