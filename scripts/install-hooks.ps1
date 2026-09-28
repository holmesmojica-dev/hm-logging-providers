[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) { throw 'The hook installer must run within the repository.' }
$hook = Join-Path $root 'hooks/pre-commit'
if (-not (Test-Path -LiteralPath $hook -PathType Leaf)) { throw "Pre-commit hook was not found at '$hook'." }
if ($env:OS -ne 'Windows_NT') {
    & chmod +x $hook
    if ($LASTEXITCODE -ne 0) { throw 'Unable to make the pre-commit hook executable.' }
}
& git -C $root config --local core.hooksPath hooks
if ($LASTEXITCODE -ne 0) { throw 'Unable to configure repository-local Git hooks.' }
Write-Output 'Repository pre-commit validation is enabled through core.hooksPath=hooks.'
