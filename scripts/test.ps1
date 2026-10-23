param([switch]$Integration)
$ErrorActionPreference = "Stop"
$project_path = Split-Path -Parent $PSScriptRoot
$output_path = Join-Path $project_path "dist"
$compiler_path = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source_paths = @(Get-ChildItem -LiteralPath (Join-Path $project_path "src") -Filter "*.cs" | ForEach-Object { $_.FullName })
$source_paths += Join-Path $project_path "tests\tray_tests.cs"
$test_path = Join-Path $output_path "Muse.Tests.exe"
$icon_path = Join-Path $output_path "muse.ico"
$manifest_path = Join-Path $project_path "src\app.manifest"
$core_path = Join-Path $output_path "Microsoft.Web.WebView2.Core.dll"
$forms_path = Join-Path $output_path "Microsoft.Web.WebView2.WinForms.dll"
& $compiler_path /nologo /target:winexe /main:MuseTests /platform:x64 /utf8output /codepage:65001 "/out:$test_path" "/win32icon:$icon_path" "/win32manifest:$manifest_path" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Core.dll /reference:System.Security.dll "/reference:$core_path" "/reference:$forms_path" $source_paths
if ($LASTEXITCODE -ne 0) { throw "Tray test compilation failed." }
$test_arguments = @()
if ($Integration) { $test_arguments = @("--integration") }
$start_options = @{FilePath=$test_path; PassThru=$true; WindowStyle="Normal"}
if ($test_arguments.Count -gt 0) { $start_options.ArgumentList = $test_arguments }
$test_process = Start-Process @start_options
if (!$test_process.WaitForExit(50000)) {
    $test_process.Kill()
    throw "Muse tray test timed out."
}
$report_name = if ($Integration) { "integration-test.txt" } else { "tray-test.txt" }
$report_path = Join-Path $output_path $report_name
Get-Content -LiteralPath $report_path -Encoding UTF8
if ($test_process.ExitCode -ne 0) { throw "Muse tray test failed." }
