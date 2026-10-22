$ErrorActionPreference = "Stop"
$project_path = Split-Path -Parent $PSScriptRoot
$executable_path = Join-Path $project_path "dist\Muse.exe"
if (!(Test-Path -LiteralPath $executable_path)) {
    throw "请先运行 scripts\build.ps1 生成 Muse.exe。"
}
$desktop_path = [Environment]::GetFolderPath("Desktop")
$shortcut_path = Join-Path $desktop_path "Muse.lnk"
if (Test-Path -LiteralPath $shortcut_path) {
    throw "桌面已有 Muse 快捷方式，未覆盖：$shortcut_path"
}
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut($shortcut_path)
    try {
        $shortcut.TargetPath = $executable_path
        $shortcut.WorkingDirectory = Split-Path -Parent $executable_path
        $shortcut.IconLocation = "$executable_path,0"
        $shortcut.Description = "Muse 桌面客户端 · 关闭进入托盘"
        $shortcut.Save()
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
    }
}
finally {
    [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
}
Write-Output $shortcut_path
