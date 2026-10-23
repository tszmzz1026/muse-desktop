$ErrorActionPreference = "Stop"
$project_path = Split-Path -Parent $PSScriptRoot
$output_path = Join-Path $project_path "dist"
$compiler_path = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source_paths = @(Get-ChildItem -LiteralPath (Join-Path $project_path "src") -Filter "*.cs" | ForEach-Object { $_.FullName })
$source_paths += Join-Path $project_path "tests\login_persistence_tests.cs"
$test_path = Join-Path $output_path "Muse.Login.Tests.exe"
$core_path = Join-Path $output_path "Microsoft.Web.WebView2.Core.dll"
$forms_path = Join-Path $output_path "Microsoft.Web.WebView2.WinForms.dll"
& $compiler_path /nologo /target:winexe /main:LoginPersistenceTests /platform:x64 /utf8output /codepage:65001 "/out:$test_path" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Core.dll /reference:System.Security.dll "/reference:$core_path" "/reference:$forms_path" $source_paths
if ($LASTEXITCODE -ne 0) { throw "Persistence test compilation failed." }
$fixture_path = Join-Path $output_path ("login-test-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $fixture_path -Force | Out-Null
$data_path = Join-Path $fixture_path "profile"
foreach ($phase in @("write", "read", "write-abrupt", "read-abrupt", "logout", "read-logout", "read-corrupt")) {
    if ($phase -eq "read-corrupt") { [System.IO.File]::WriteAllBytes((Join-Path $data_path "session-cookies.dat"), [byte[]]@(1, 2, 3, 4)) }
    $report_path = Join-Path $fixture_path "$phase.txt"
    $arguments = "$phase `"$data_path`" `"$report_path`""
    $test_process = Start-Process -FilePath $test_path -ArgumentList $arguments -WindowStyle Normal -PassThru
    if (!$test_process.WaitForExit(35000)) { $test_process.Kill(); throw "Persistence test timed out: $phase" }
    Get-Content -LiteralPath $report_path -Encoding UTF8
    if ($test_process.ExitCode -ne 0) { throw "Login persistence test failed: $phase" }
    if ($phase -eq "write" -or $phase -eq "write-abrupt") {
        $cache_path = Join-Path $data_path "session-cookies.dat"
        if (!(Test-Path -LiteralPath $cache_path)) { throw "Encrypted session checkpoint is missing." }
        $cache_text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($cache_path))
        if ($cache_text.Contains("fixture-session") -or $cache_text.Contains("fixture-refreshed")) { throw "Session values were stored as plaintext." }
    }
}
if (Test-Path -LiteralPath (Join-Path $data_path "session-cookies.dat")) { throw "Logout did not clear the saved session." }
Write-Output "PASS: normal restart, abrupt exit, current-user encrypted storage, preserved cookie attributes, explicit logout, and damaged-cache recovery."
