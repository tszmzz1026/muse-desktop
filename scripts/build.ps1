$ErrorActionPreference = "Stop"
$project_path = Split-Path -Parent $PSScriptRoot
$output_path = Join-Path $project_path "dist"
New-Item -ItemType Directory -Path $output_path -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$icon_path = Join-Path $output_path "muse.ico"
$icon_stream = [System.IO.File]::Create($icon_path)
$icon_writer = [System.IO.BinaryWriter]::new($icon_stream)
try {
    $icon_writer.Write([uint16]0)
    $icon_writer.Write([uint16]1)
    $icon_writer.Write([uint16]1)
    $bitmap = [System.Drawing.Bitmap]::new(64, 64)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(239, 233, 255))
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(29, 24, 48), 7)
    $image_stream = [System.IO.MemoryStream]::new()
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::FromArgb(29, 24, 48))
        $graphics.FillEllipse($brush, 3, 3, 58, 58)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $points = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(18, 43),
            [System.Drawing.PointF]::new(18, 23),
            [System.Drawing.PointF]::new(32, 36),
            [System.Drawing.PointF]::new(46, 23),
            [System.Drawing.PointF]::new(46, 43)
        )
        $graphics.DrawLines($pen, $points)
        $bitmap.Save($image_stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $image_bytes = $image_stream.ToArray()
        $icon_writer.Write([byte]64)
        $icon_writer.Write([byte]64)
        $icon_writer.Write([byte]0)
        $icon_writer.Write([byte]0)
        $icon_writer.Write([uint16]1)
        $icon_writer.Write([uint16]32)
        $icon_writer.Write([uint32]$image_bytes.Length)
        $icon_writer.Write([uint32]22)
        $icon_writer.Write($image_bytes)
    }
    finally {
        $image_stream.Dispose()
        $pen.Dispose()
        $brush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
finally {
    $icon_writer.Dispose()
    $icon_stream.Dispose()
}
$compiler_path = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path -LiteralPath $compiler_path)) {
    throw "未找到 .NET Framework C# 编译器。"
}
$sdk_path = & (Join-Path $PSScriptRoot "restore_dependencies.ps1")
$core_path = Join-Path $sdk_path "Microsoft.Web.WebView2.Core.dll"
$forms_path = Join-Path $sdk_path "Microsoft.Web.WebView2.WinForms.dll"
$build_filename = if ($env:MUSE_BUILD_FILENAME) { $env:MUSE_BUILD_FILENAME } else { "Muse.exe" }
if ($build_filename -notin @("Muse.exe", "Muse.next.exe")) { throw "Unsupported build filename." }
$executable_path = Join-Path $output_path $build_filename
$source_paths = @(Get-ChildItem -LiteralPath (Join-Path $project_path "src") -Filter "*.cs" | ForEach-Object { $_.FullName })
$manifest_path = Join-Path $project_path "src\app.manifest"
& $compiler_path /nologo /target:winexe /platform:x64 /optimize+ /utf8output /codepage:65001 "/out:$executable_path" "/win32icon:$icon_path" "/win32manifest:$manifest_path" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$core_path" "/reference:$forms_path" $source_paths
if ($LASTEXITCODE -ne 0) {
    throw "Muse 编译失败，退出码：$LASTEXITCODE"
}
Write-Output $executable_path
