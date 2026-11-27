param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('add', 'list', 'rm', 'key')]
    [string]$Action,
    [string]$InputFile,
    [string]$Python,
    [string]$HermesHome
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$env:PYTHONIOENCODING = 'utf-8'

if (-not [string]::IsNullOrWhiteSpace($HermesHome)) {
    $env:HERMES_HOME = $HermesHome
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$helper = Join-Path $scriptDir 'vaultforge_helper.py'
if (-not (Test-Path -LiteralPath $helper)) {
    Write-Error "vaultforge_helper.py tidak ditemukan di $scriptDir"
    exit 2
}

if ([string]::IsNullOrWhiteSpace($Python)) { $Python = 'python.exe' }
if (-not (Test-Path -LiteralPath $Python)) {
    $cmd = Get-Command $Python -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        Write-Error "Python tidak ditemukan: $Python"
        exit 3
    }
    $Python = $cmd.Source
}

$argsList = @($helper, $Action)
if (-not [string]::IsNullOrWhiteSpace($InputFile)) {
    $argsList += $InputFile
}

& $Python @argsList
exit $LASTEXITCODE
