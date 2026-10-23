param(
    [ValidatePattern("^\d+\.\d+\.\d+$")][string]$Version = "1.0.1",
    [switch]$Installer
)
$ErrorActionPreference = "Stop"
$project_path = Split-Path -Parent $PSScriptRoot
$release_path = Join-Path $project_path "release"
$test_path = Join-Path $release_path ("verification-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $test_path -Force | Out-Null
$zip_path = Join-Path $release_path "Muse-Desktop-$Version-win-x64.zip"
Expand-Archive -LiteralPath $zip_path -DestinationPath (Join-Path $test_path "portable")
$portable_path = Join-Path $test_path "portable\Muse-Desktop"
function Test-AppRuntime([string]$AppPath) {
    $app_process = Start-Process -FilePath (Join-Path $AppPath "Muse.exe") -ArgumentList "--diagnose" -WindowStyle Hidden -PassThru -Wait
    if ($app_process.ExitCode -ne 0) { throw "Packaged application runtime check failed." }
    $diagnostics = Get-Content -LiteralPath (Join-Path $AppPath "diagnostics.json") -Raw | ConvertFrom-Json
    if ($diagnostics.mode -ne "native_webview2" -or !$diagnostics.runtime_version) { throw "Packaged application is not using WebView2." }
}
if ($Installer) {
    if (@(Get-Process -Name Muse -ErrorAction SilentlyContinue).Count -gt 0) { throw "Exit Muse before testing the installer." }
    $install_path = Join-Path $test_path "installed"
    $setup_path = Join-Path $release_path "Muse-Desktop-$Version-Setup.exe"
    $setup_arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /TASKS="" /DIR="' + $install_path + '" /LOG="' + (Join-Path $test_path "install.log") + '"'
    $setup_process = Start-Process -FilePath $setup_path -ArgumentList $setup_arguments -WindowStyle Hidden -PassThru -Wait
    if ($setup_process.ExitCode -ne 0) { throw "Setup failed with exit code $($setup_process.ExitCode)." }
    Test-AppRuntime $install_path
    $uninstaller_process = Start-Process -FilePath (Join-Path $install_path "unins000.exe") -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -WindowStyle Hidden -PassThru -Wait
    if ($uninstaller_process.ExitCode -ne 0) { throw "Uninstall failed." }
    if (Test-Path -LiteralPath (Join-Path $install_path "Muse.exe")) { throw "Uninstall did not remove application files." }
}
Test-AppRuntime $portable_path
$checked_test_path = [System.IO.Path]::GetFullPath($test_path)
$checked_release_prefix = [System.IO.Path]::GetFullPath($release_path).TrimEnd("\") + "\"
if (!$checked_test_path.StartsWith($checked_release_prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Unexpected test directory." }
Remove-Item -LiteralPath $checked_test_path -Recurse -Force
if ($Installer) { Write-Output "PASS: installer, installed application, uninstall, and portable application." }
else { Write-Output "PASS: portable package loads WebView2 with only its distributed files." }
