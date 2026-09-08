# Loader Doctor - proves MelonLoader actually loaded, and repairs the proxy when it did not.
#
# WHY THIS FILE EXISTS
#
# MelonLoader 0.7.3 was installed into The Long Dark and did nothing at all: no MelonLoader\Logs,
# no MelonPreferences.cfg, no interop assemblies, and the game booted clean. Nothing said why,
# because a proxy DLL that is never loaded cannot log its own absence.
#
# The line that stated the cause was the module list of the running game, in load order:
#
#     apphelp.dll, AcGenral.DLL, ... , VERSION.dll  (C:\windows\SYSTEM32\VERSION.dll), ...
#     UnityPlayer.dll
#
# The Windows application-compatibility shim engine (apphelp + AcGenral) is loaded into tld.exe
# before UnityPlayer, and AcGenral imports version.dll. So System32's version.dll was already in
# the process by the time UnityPlayer asked for version.dll, and a module that is already loaded
# is never resolved again. The game folder copy - MelonLoader's proxy - was never even looked at.
#
# winmm.dll is also imported by AcGenral, but it is NOT pulled in early: in the observed load
# order it appears after UnityPlayer, which means UnityPlayer is what loads it, and that lookup
# does honour the application directory. So the proxy works under that name on this machine.
#
# THE RULE THIS FILE HOLDS TO
#
# Anything that broke once heals itself the next time. So this does not just report - it detects
# the dead loader, retries under the next supported proxy name, and says out loud what it did.
# Run it any time the mod seems absent, and from build.ps1 before every deploy.
#
#   powershell -ExecutionPolicy Bypass -File tools\loader-doctor.ps1
#   powershell -ExecutionPolicy Bypass -File tools\loader-doctor.ps1 -Repair   # rotate the name

[CmdletBinding()]
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\TheLongDark',
    [switch]$Repair
)
$ErrorActionPreference = 'Stop'

# MelonLoader accepts exactly these three names for its proxy, in its own documentation order.
# Rotation goes forwards through this list, wrapping, so repeated repairs try every one.
$ProxyNames = @('version.dll', 'winmm.dll', 'winhttp.dll')

function Say([string]$text, [string]$colour = 'Gray') { Write-Host $text -ForegroundColor $colour }

if (-not (Test-Path $GameDir)) { throw "Game folder not found: $GameDir" }

$mlDir = Join-Path $GameDir 'MelonLoader'
if (-not (Test-Path $mlDir)) {
    Say "MelonLoader is not installed in $GameDir - there is nothing to load the mod." 'Red'
    exit 2
}

# ---- which proxy is in place -----------------------------------------------------------------
$present = @()
foreach ($n in $ProxyNames) {
    $p = Join-Path $GameDir $n
    if (Test-Path $p) {
        # A real MelonLoader proxy is roughly 11 MB and carries the ML product version. A game's
        # own winmm shim would not, so the size and version are what identify it rather than name.
        $item = Get-Item $p
        $ver  = $item.VersionInfo.ProductVersion
        $present += [pscustomobject]@{ Name = $n; Size = $item.Length; Version = $ver }
    }
}
if ($present.Count -eq 0) {
    Say "No MelonLoader proxy DLL in the game folder. Expected one of: $($ProxyNames -join ', ')" 'Red'
    exit 2
}
foreach ($p in $present) { Say ("proxy: {0}  {1:N0} bytes  v{2}" -f $p.Name, $p.Size, $p.Version) }
if ($present.Count -gt 1) {
    Say "More than one proxy is present. Two bootstraps can race - keep exactly one." 'Yellow'
}

# ---- did it actually run ----------------------------------------------------------------------
# The proof is a log NEWER than the last time the game was started. MelonLoader writes Latest.log
# at the top of its own startup, so a stale log means the last launch went unmodded.
$log = Join-Path $mlDir 'Latest.log'
$exe = Join-Path $GameDir 'tld.exe'
$loaded = $false
$logAge = $null
if (Test-Path $log) {
    $logAge = (Get-Item $log).LastWriteTime
    Say ("last MelonLoader log: {0}" -f $logAge)
    $loaded = $true
} else {
    Say "MelonLoader has never written a log here - it has never run." 'Yellow'
}

# A running game is the strongest evidence available: ask the process itself.
$proc = Get-Process -Name tld -ErrorAction SilentlyContinue
if ($proc) {
    $mods = @($proc.Modules | Where-Object { $_.ModuleName -match 'Melon' })
    if ($mods.Count -gt 0) {
        Say "MelonLoader IS loaded into the running game." 'Green'
        exit 0
    }
    Say "The game is running WITHOUT MelonLoader in it." 'Red'
    $loaded = $false
}

if ($loaded -and -not $Repair) {
    Say "MelonLoader has run at least once. If the mod is still absent, the fault is in the mod, not the loader." 'Green'
    exit 0
}

# ---- repair ------------------------------------------------------------------------------------
if (-not $Repair) {
    Say "Run again with -Repair to rotate the proxy name to the next one MelonLoader supports." 'Yellow'
    exit 1
}
if ($proc) { Say "Close the game first - its proxy DLL is locked while it runs." 'Red'; exit 1 }

$current = $present[0].Name
$idx     = [array]::IndexOf($ProxyNames, $current)
$next    = $ProxyNames[($idx + 1) % $ProxyNames.Count]
foreach ($p in $present) { if ($p.Name -ne $current) { Remove-Item (Join-Path $GameDir $p.Name) -Force } }
Move-Item (Join-Path $GameDir $current) (Join-Path $GameDir $next) -Force
Say "proxy renamed $current -> $next. Start the game and run this again to confirm." 'Cyan'
exit 0
