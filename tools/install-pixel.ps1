# Builds a debug APK, installs it on the phone plugged in over USB (never the emulator) and opens the game.
# Run from any folder:  powershell -ExecutionPolicy Bypass -File tools\install-pixel.ps1
param(
    # How long to wait for the phone to show up before giving up
    [int]$WaitSeconds = 90
)

$root = Split-Path -Parent $PSScriptRoot
$package = 'com.spoonergameindustries.mergedcells'
$apk = Join-Path $root 'builds\android\MergedCells.apk'
$adb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
$godot = 'Godot_v4.7.2-stable_mono_win64_console.exe'

function Stop-WithError([string]$message) {
    Write-Host $message -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $adb)) {
    Stop-WithError "adb isn't at $adb."
}
if (-not (Get-Command $godot -ErrorAction SilentlyContinue)) {
    Stop-WithError "$godot isn't on PATH."
}

# Without these, an export whose output is captured hangs after finishing (MSBuild nodes keep the pipe open)
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:UseSharedCompilation = 'false'

# Start adb detached first, or Godot starts its own server, which inherits the output pipe and never lets go
(Start-Process -FilePath $adb -ArgumentList 'start-server' -WindowStyle Hidden -PassThru).WaitForExit()

New-Item -ItemType Directory -Force (Split-Path $apk) | Out-Null
Write-Host 'Building the debug APK...'
& $godot --headless --path $root --export-debug Android $apk
# Godot 4.7.2 always ends with a harmless "EditorSettings not instantiated" error; the exit code is what counts
if ($LASTEXITCODE -ne 0) {
    Stop-WithError "The build failed (exit code $LASTEXITCODE)."
}

$serial = $null
$hint = $null
$deadline = (Get-Date).AddSeconds($WaitSeconds)
while (-not $serial) {
    $phones = @(& $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\S' -and $_ -notmatch '^emulator-' })
    $ready = $phones | Where-Object { $_ -match '\tdevice$' } | Select-Object -First 1
    if ($ready) {
        $serial = ($ready -split '\t')[0]
        break
    }
    $state = if ($phones -match '\tunauthorized$') { 'unauthorized' } else { 'missing' }
    if ($state -ne $hint) {
        if ($state -eq 'unauthorized') {
            Write-Host 'On the phone, tap Allow on "Allow USB debugging?".' -ForegroundColor Yellow
        }
        else {
            Write-Host 'Waiting for the phone: plug it in, unlock it, and check USB debugging is on.' -ForegroundColor Yellow
        }
        $hint = $state
    }
    if ((Get-Date) -gt $deadline) {
        Stop-WithError "No phone showed up in $WaitSeconds seconds. The APK is built at $apk."
    }
    Start-Sleep -Seconds 2
}

$model = (& $adb -s $serial shell getprop ro.product.model | Out-String).Trim()
Write-Host "Installing on $model ($serial)..."
for ($attempt = 1; ; $attempt++) {
    # cmd merges adb's error stream, which Windows PowerShell would otherwise wrap in error records. Only the phone's current
    # user: adb's default puts a copy in every user (the Pixel has a "Temp" one), and a copy left there by a debug build
    # blocks the playtest release, signed with another key, after the developer uninstalls it from their own profile
    $output = cmd /c "`"$adb`" -s $serial install -r --user current `"$apk`" 2>&1" | Out-String
    if ($LASTEXITCODE -eq 0) {
        break
    }
    # A playtest release on the phone fails as a downgrade first (its version code is the CI run's; this build's is 1)
    if ($output -match 'INSTALL_FAILED_UPDATE_INCOMPATIBLE|INSTALL_FAILED_VERSION_DOWNGRADE') {
        Stop-WithError ("The phone has a copy from a newer build or signed with a different key (the playtest release?), " +
            "maybe in another of its users. Uninstall it for every user first (`"$adb`" -s $serial uninstall $package), " +
            "which deletes its levels and progress, then run this again.")
    }
    if ($attempt -ge 3) {
        Stop-WithError "The install failed three times:`n$output"
    }
    Write-Host 'The install failed; trying again in a few seconds...' -ForegroundColor Yellow
    Start-Sleep -Seconds 3
}

cmd /c "`"$adb`" -s $serial shell monkey -p $package -c android.intent.category.LAUNCHER 1 >NUL 2>&1"
Write-Host "Done: the latest build is installed and open on $model." -ForegroundColor Green
