# =====================================================================
# check-phone.ps1
# Check that an Android phone is connected and ready for `dotnet run`.
# Usage:  .\tools\check-phone.ps1
# =====================================================================

$ErrorActionPreference = 'Continue'

function Write-Header($text) {
    Write-Host ""
    Write-Host "=== $text ===" -ForegroundColor Cyan
}

function Write-Ok($text) {
    Write-Host "  [OK] $text" -ForegroundColor Green
}

function Write-Warn($text) {
    Write-Host "  [!]  $text" -ForegroundColor Yellow
}

function Write-Err($text) {
    Write-Host "  [X]  $text" -ForegroundColor Red
}

# ---------------------------------------------------------------------
# 1. Locate adb.exe
# ---------------------------------------------------------------------
Write-Header "Locating adb.exe"

$adbCandidates = @(
    "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
    "$env:ProgramFiles\Android\Sdk\platform-tools\adb.exe",
    "${env:ProgramFiles(x86)}\Android\Sdk\platform-tools\adb.exe"
)

$adb = $adbCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $adb) {
    $adbInPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($adbInPath) { $adb = $adbInPath.Source }
}

if (-not $adb) {
    Write-Err "adb.exe not found. Make sure Android SDK is installed."
    Write-Host "    Expected paths:" -ForegroundColor DarkGray
    $adbCandidates | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
    exit 1
}

Write-Ok "Found: $adb"

# ---------------------------------------------------------------------
# 2. Start adb server and list devices
# ---------------------------------------------------------------------
Write-Header "Connected devices"

& $adb start-server | Out-Null
Start-Sleep -Milliseconds 300

$deviceLines = & $adb devices | Select-Object -Skip 1 | Where-Object { $_.Trim() -ne '' }

if (-not $deviceLines) {
    Write-Err "No phone connected."
    Write-Host ""
    Write-Host "  Checklist:" -ForegroundColor DarkGray
    Write-Host "    1. USB cable is plugged into the PC (must support data transfer)" -ForegroundColor DarkGray
    Write-Host "    2. 'USB debugging' is enabled on the phone" -ForegroundColor DarkGray
    Write-Host "    3. Phone screen is unlocked" -ForegroundColor DarkGray
    exit 2
}

$hasConnected = $false
foreach ($line in $deviceLines) {
    $parts = $line -split '\s+'
    if ($parts.Count -lt 2) { continue }

    $serial = $parts[0]
    $status = $parts[1]

    switch ($status) {
        'device' {
            $hasConnected = $true
            Write-Ok "Device $serial - connected"
        }
        'unauthorized' {
            Write-Warn "Device $serial - unauthorized. Unlock the phone and confirm USB debugging."
        }
        'offline' {
            Write-Warn "Device $serial - offline. Replug the cable or run 'adb kill-server'."
        }
        default {
            Write-Warn "Device $serial - status: $status"
        }
    }
}

if (-not $hasConnected) {
    Write-Host ""
    Write-Err "No device is ready."
    exit 3
}

# ---------------------------------------------------------------------
# 3. Device info
# ---------------------------------------------------------------------
Write-Header "Device info"

$model   = (& $adb shell getprop ro.product.model).Trim()
$android = (& $adb shell getprop ro.build.version.release).Trim()
$sdk     = (& $adb shell getprop ro.build.version.sdk).Trim()
$battery = (& $adb shell dumpsys battery | Select-String "level" | Select-Object -First 1)

Write-Host "  Model:           $model"
Write-Host "  Android:         $android (API $sdk)"
if ($battery) { Write-Host "  Battery:         $($battery.ToString().Trim())" }

# ---------------------------------------------------------------------
# 4. Is AFi installed?
# ---------------------------------------------------------------------
Write-Header "AFi app on device"

$packageName = "com.companyname.afi"
$installedPkg = (& $adb shell pm list packages $packageName) 2>$null

if ($installedPkg -match $packageName) {
    $versionLine = (& $adb shell dumpsys package $packageName | Select-String "versionName" | Select-Object -First 1)
    Write-Ok "Installed - $($versionLine.ToString().Trim())"
} else {
    Write-Warn "Not installed. First run: dotnet build -t:Run -f net10.0-android"
}

# ---------------------------------------------------------------------
# 5. Done
# ---------------------------------------------------------------------
Write-Header "Ready"

Write-Host "  You can now run:" -ForegroundColor Green
Write-Host "    cd C:\AFi\AFi" -ForegroundColor White
Write-Host "    dotnet build -t:Run -f net10.0-android" -ForegroundColor White
Write-Host ""