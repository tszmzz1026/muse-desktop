$ErrorActionPreference = "Stop"
$project_path = Split-Path -Parent $PSScriptRoot
$package_version = "1.0.4258.31"
$packages_path = Join-Path $project_path "packages"
$package_path = Join-Path $packages_path "microsoft.web.webview2.$package_version"
$sdk_path = Join-Path $package_path "lib\net462"
if (!(Test-Path -LiteralPath (Join-Path $sdk_path "Microsoft.Web.WebView2.WinForms.dll"))) {
    New-Item -ItemType Directory -Path $packages_path -Force | Out-Null
    $archive_path = Join-Path $packages_path "microsoft.web.webview2.$package_version.zip"
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$package_version/microsoft.web.webview2.$package_version.nupkg" -OutFile $archive_path
    Expand-Archive -LiteralPath $archive_path -DestinationPath $package_path -Force
}
$output_path = Join-Path $project_path "dist"
New-Item -ItemType Directory -Path $output_path -Force | Out-Null
foreach ($source_path in @(
    (Join-Path $sdk_path "Microsoft.Web.WebView2.Core.dll"),
    (Join-Path $sdk_path "Microsoft.Web.WebView2.WinForms.dll"),
    (Join-Path $package_path "runtimes\win-x64\native\WebView2Loader.dll")
)) {
    $destination_path = Join-Path $output_path ([System.IO.Path]::GetFileName($source_path))
    if (!(Test-Path -LiteralPath $destination_path) -or (Get-FileHash -LiteralPath $source_path).Hash -ne (Get-FileHash -LiteralPath $destination_path).Hash) {
        Copy-Item -LiteralPath $source_path -Destination $destination_path -Force
    }
}
Write-Output $sdk_path
