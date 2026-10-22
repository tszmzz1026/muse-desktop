param(
    [ValidatePattern("^\d+\.\d+\.\d+$")][string]$Version = "1.0.0",
    [ValidateSet("Muse.exe", "Muse.next.exe")][string]$Executable = "Muse.exe",
    [string]$InstallerCompiler
)
$ErrorActionPreference = "Stop"
$project_path = Split-Path -Parent $PSScriptRoot
$dist_path = Join-Path $project_path "dist"
$release_path = Join-Path $project_path "release"
$tools_path = Join-Path $project_path "tools"
New-Item -ItemType Directory -Path $release_path -Force | Out-Null
New-Item -ItemType Directory -Path $tools_path -Force | Out-Null
$bootstrapper_path = Join-Path $tools_path "MicrosoftEdgeWebview2Setup.exe"
if (!(Test-Path -LiteralPath $bootstrapper_path)) {
    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $bootstrapper_path
}
$signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper_path
if ($signature.Status -ne "Valid" -or $signature.SignerCertificate.Subject -notlike "*Microsoft Corporation*") {
    throw "Microsoft WebView2 bootstrapper signature is invalid."
}
$staging_path = Join-Path $release_path ("staging-" + [Guid]::NewGuid().ToString("N"))
$bundle_path = Join-Path $staging_path "Muse-Desktop"
New-Item -ItemType Directory -Path $bundle_path -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $dist_path $Executable) -Destination (Join-Path $bundle_path "Muse.exe")
foreach ($runtime_file in @("Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.WinForms.dll", "WebView2Loader.dll", "muse.ico")) {
    Copy-Item -LiteralPath (Join-Path $dist_path $runtime_file) -Destination $bundle_path
}
Copy-Item -LiteralPath $bootstrapper_path -Destination $bundle_path
Copy-Item -LiteralPath (Join-Path $project_path "docs\DOWNLOADS.md") -Destination (Join-Path $bundle_path "README.md")
Copy-Item -LiteralPath (Join-Path $project_path "THIRD_PARTY_NOTICES.txt") -Destination $bundle_path
$zip_path = Join-Path $release_path "Muse-Desktop-$Version-win-x64.zip"
Compress-Archive -LiteralPath $bundle_path -DestinationPath $zip_path -Force
if (!$InstallerCompiler) {
    $candidates = @(
        (Join-Path $tools_path "innosetup\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe")
    )
    $InstallerCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$InstallerCompiler) { throw "Inno Setup compiler was not found. Pass -InstallerCompiler with the path to ISCC.exe." }
& $InstallerCompiler /Qp "/DAppVersion=$Version" "/DSourceDir=$bundle_path" "/DReleaseDir=$release_path" (Join-Path $PSScriptRoot "installer.iss")
if ($LASTEXITCODE -ne 0) { throw "Windows installer build failed." }
$setup_path = Join-Path $release_path "Muse-Desktop-$Version-Setup.exe"
$checksum_lines = @($zip_path, $setup_path) | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($_))"
}
$checksum_lines | Set-Content -LiteralPath (Join-Path $release_path "SHA256SUMS.txt") -Encoding ascii
$checked_staging_path = [System.IO.Path]::GetFullPath($staging_path)
$checked_release_prefix = [System.IO.Path]::GetFullPath($release_path).TrimEnd("\") + "\"
if (!$checked_staging_path.StartsWith($checked_release_prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Unexpected staging path." }
Remove-Item -LiteralPath $checked_staging_path -Recurse -Force
Get-Item -LiteralPath $zip_path, $setup_path | Select-Object FullName, Length
