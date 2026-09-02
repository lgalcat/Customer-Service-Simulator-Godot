#Requires -Version 5.1
<#
    run-tests.ps1 - preferred local / agent entry point for the gdUnit4 suite.

    Prints an up-front warning (some suites simulate keyboard/mouse through the OS-global Input
    state, so real input during the run causes false failures), pauses briefly so it can be
    aborted, then hands off to `dotnet test` with any arguments passed through.

    Examples:
      tools\run-tests.ps1
      tools\run-tests.ps1 --filter "FullyQualifiedName~PlayerTesting"
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetTestArgs
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$sensitive = @(
    'PlayerTesting', 'PlatformerBehaviourTesting', 'ThrowPaperBallBehaviourTesting',
    'CardTesting', 'SwatterTesting', 'SolitaireBehaviourTesting',
    'BallTesting', 'GoalTesting', 'FlyTesting', 'FlySpawnerTesting',
    'TrashCanTesting', 'FlySwatterBehaviourTesting'
)

Write-Host ''
Write-Host '================================================================================' -ForegroundColor Yellow
Write-Host '  gdUnit4 TEST RUN - INPUT/TIMING-SENSITIVE SUITES INCLUDED' -ForegroundColor Yellow
Write-Host '================================================================================' -ForegroundColor Yellow
Write-Host '  Some suites simulate keyboard/mouse through the OS-global Input state. Real'
Write-Host '  keyboard/mouse input on this machine during the run will cause false failures.'
Write-Host '  Close input-generating apps and keep hands off until it finishes (~a few min).'
Write-Host ''
Write-Host ('  Sensitive suites: {0}' -f ($sensitive -join ', '))
Write-Host ''
Write-Host '  A warning window shows while the run is active. Suppress it with' -ForegroundColor DarkGray
Write-Host '  $env:GDUNIT_NO_INPUT_POPUP = 1' -ForegroundColor DarkGray
Write-Host '================================================================================' -ForegroundColor Yellow

for ($i = 5; $i -ge 1; $i--) {
    Write-Host ("`r  Starting in {0}s ... (Ctrl-C to abort) " -f $i) -NoNewline -ForegroundColor Yellow
    Start-Sleep -Seconds 1
}
Write-Host "`r  Starting now.                              " -ForegroundColor Yellow
Write-Host ''

Push-Location $root
try {
    & dotnet test @DotnetTestArgs
    $code = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $code
