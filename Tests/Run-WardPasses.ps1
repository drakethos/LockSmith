<#
.SYNOPSIS
  Runs LockSmith's ward-mod integration suites, switching WardIsLove / Arcane Ward / Protective Wards off and on between passes.

.DESCRIPTION
  A mod is "off" while its DLLs are renamed to *.dll.old (what Gale does), so the profile's mod list is untouched and
  everything is put back at the end, even on Ctrl+C. Passes:
    Core             no ward mod            -> LockSmith, -Menu, -PieceMode, -Net (the full vanilla regression)
    None             no ward mod            -> suite LockSmith              (vanilla ward regression)
    WardIsLove       only WardIsLove        -> suite LockSmith-WardIsLove
    ArcaneWard       only Arcane Ward       -> suite LockSmith-ArcaneWard
    ProtectiveWards  only Protective Wards  -> suite LockSmith-ProtectiveWards
    All              all three together     -> the three ward suites
  Results of each pass are copied to <OutDir>\<pass>\ (latest.json / latest.html).
  Build first:  dotnet msbuild LockSmith.csproj -p:ProfileName=drakeWardTest

.EXAMPLE
  .\Run-WardPasses.ps1
  .\Run-WardPasses.ps1 -Passes WardIsLove,ArcaneWard
#>
[CmdletBinding()]
param(
    [string]$Profile = 'drakeWardTest',
    # comma separated (powershell -File passes it as one string): None, WardIsLove, ArcaneWard, ProtectiveWards, All
    [string]$Passes = 'None,WardIsLove,ArcaneWard,ProtectiveWards,All',
    [string]$OutDir = '',
    [int]$TimeoutSec = 900,
    [ValidateSet('', 'Off', 'Failures', 'Always')]
    [string]$Screenshots = 'Failures',
    # Valheim launch queue: -NoQueue skips it, -Priority goes next (only when the user says skip the line)
    [switch]$NoQueue,
    [switch]$Priority,
    [string]$QueueName = ''
)

$ErrorActionPreference = 'Stop'

# One Valheim at a time (DrakesWorkshop/scripts/Valheim-Queue.ps1): wait in the shared first-come-first-served queue and
# hold the place for this whole run (builds and launches included). -Priority = goes next, only when the user says so.
# Nested runs see VALHEIM_QUEUE_HELD and do not queue again; without the queue script (outside the dev tree) it just runs.
$valheimQueue = Join-Path $PSScriptRoot '..\..\DrakesWorkshop\scripts\Valheim-Queue.ps1'
if (-not $NoQueue -and -not $env:VALHEIM_QUEUE_HELD -and (Test-Path -LiteralPath $valheimQueue)) {
    $env:VALHEIM_QUEUE_HELD = '1'
    $passOn = foreach ($p in $PSBoundParameters.GetEnumerator()) {
        if ($p.Key -in 'NoQueue', 'Priority', 'QueueName') { continue }
        if ($p.Value -is [System.Management.Automation.SwitchParameter]) { if ($p.Value.IsPresent) { "-$($p.Key)" } }
        elseif ($p.Value -is [array]) { "-$($p.Key) @($(($p.Value | ForEach-Object { "'$(([string]$_) -replace "'", "''")'" }) -join ','))" }
        else { "-$($p.Key) '$(([string]$p.Value) -replace "'", "''")'" }
    }
    $who = if ($QueueName) { $QueueName } else { "LockSmith ward passes $($PSBoundParameters['Suites'])$($PSBoundParameters['Mods'])$($PSBoundParameters['Passes'])".Trim() }
    try { & $valheimQueue run -Name $who -Priority:$Priority -Command "& '$PSCommandPath' $($passOn -join ' ')" }
    finally { $queueCode = $LASTEXITCODE; Remove-Item Env:VALHEIM_QUEUE_HELD -ErrorAction SilentlyContinue }
    exit $queueCode
}
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($OutDir -eq '') { $OutDir = Join-Path $here 'ward-pass-results' }
$profilePath = Join-Path $env:APPDATA "com.kesomannen.gale\valheim\profiles\$Profile"
$plugins = Join-Path $profilePath 'BepInEx\plugins'
$results = Join-Path $profilePath 'BepInEx\config\DrakesTestLab\results'
$runner = Join-Path $here '..\..\DrakesWorkshop\DrakesTestLab\tools\Run-TestLabSuites.ps1'

$wardMods = [ordered]@{
    WardIsLove      = 'Azumatt-WardIsLove'
    ArcaneWard      = 'KG-Arcane_Ward'
    ProtectiveWards = 'shudnal-ProtectiveWards'
}
$plan = @{
    Smoke           = @{ On = @();                                              Suites = 'LockSmith/L-01,LockSmith/L-05,LockSmith-Menu/LM-02,LockSmith-Net/N-01,LockSmith-Net/N-02,LockSmith-Rpc/RP-04' }
    Core            = @{ On = @();                                              Suites = 'LockSmith,LockSmith-Menu,LockSmith-PieceMode,LockSmith-Net' }
    None            = @{ On = @();                                              Suites = 'LockSmith' }
    WardIsLove      = @{ On = @('WardIsLove');                                  Suites = 'LockSmith-WardIsLove' }
    ArcaneWard      = @{ On = @('ArcaneWard');                                  Suites = 'LockSmith-ArcaneWard' }
    ProtectiveWards = @{ On = @('ProtectiveWards');                             Suites = 'LockSmith-ProtectiveWards' }
    RpcNone            = @{ On = @();                                              Suites = 'LockSmith-Rpc' }
    RpcWardIsLove      = @{ On = @('WardIsLove');                                  Suites = 'LockSmith-Rpc-WardIsLove' }
    RpcArcaneWard      = @{ On = @('ArcaneWard');                                  Suites = 'LockSmith-Rpc-ArcaneWard' }
    RpcProtectiveWards = @{ On = @('ProtectiveWards');                             Suites = 'LockSmith-Rpc-ProtectiveWards' }
    AllArcane       = @{ On = @('WardIsLove', 'ArcaneWard', 'ProtectiveWards'); Suites = 'LockSmith-ArcaneWard' }
    All             = @{ On = @('WardIsLove', 'ArcaneWard', 'ProtectiveWards'); Suites = 'LockSmith-WardIsLove,LockSmith-ArcaneWard,LockSmith-ProtectiveWards' }
}

function Set-ModEnabled([string]$folder, [bool]$enabled) {
    $dir = Join-Path $plugins $folder
    if (-not (Test-Path -LiteralPath $dir)) { throw "Mod folder not found: $dir" }
    if ($enabled) {
        Get-ChildItem -LiteralPath $dir -Recurse -Filter '*.dll.old' | ForEach-Object { Rename-Item -LiteralPath $_.FullName -NewName ($_.Name -replace '\.old$', '') }
    } else {
        Get-ChildItem -LiteralPath $dir -Recurse -Filter '*.dll' | ForEach-Object { Rename-Item -LiteralPath $_.FullName -NewName ($_.Name + '.old') }
    }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$summary = @()
$passList = @($Passes -split '[,;\s]+' | Where-Object { $_ })
foreach ($name in $passList) { if (-not $plan.ContainsKey($name)) { throw "Unknown pass '$name' (Smoke, Core, None, WardIsLove, ArcaneWard, ProtectiveWards, All, AllArcane, RpcNone, RpcWardIsLove, RpcArcaneWard, RpcProtectiveWards)" } }
try {
    foreach ($pass in $passList) {
        $p = $plan[$pass]
        foreach ($name in $wardMods.Keys) { Set-ModEnabled $wardMods[$name] ($p.On -contains $name) }
        Write-Host "=== pass '$pass': ward mods on = [$($p.On -join ', ')]  suites = $($p.Suites)"
        if (Test-Path -LiteralPath $results) { Remove-Item -LiteralPath (Join-Path $results 'latest.*') -Force -ErrorAction SilentlyContinue }
        & $runner -Suites $p.Suites -Profile $Profile -TimeoutSec $TimeoutSec -Screenshots $Screenshots
        $code = $LASTEXITCODE
        $dest = Join-Path $OutDir $pass
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        # result files only (the run-* screenshot folders stay in the profile: their names overrun the Windows path limit here)
        if (Test-Path -LiteralPath $results) { Get-ChildItem -LiteralPath $results -File | Copy-Item -Destination $dest -Force }
        $summary += [pscustomobject]@{ Pass = $pass; ExitCode = $code }
    }
}
finally {
    foreach ($name in $wardMods.Keys) { Set-ModEnabled $wardMods[$name] $true }
    Write-Host 'All ward mods switched back on.'
}

Write-Host ''
$summary | Format-Table -AutoSize
Write-Host '(exit code: 0 = all passed, 1 = a test failed, 2 = could not start, 3 = timed out / no results)'
if ($summary | Where-Object { $_.ExitCode -ne 0 }) { exit 1 }
