# Muse Desktop 使用说明

Muse.ai 非官方 Windows 桌面客户端。适用于 Windows 10 2004 及以上版本、Windows 11，64 位系统。网站功能需要联网及你自己的 Muse 账户。

## 推荐：安装版

双击 `Muse-Desktop-1.0.0-Setup.exe`，按步骤安装。安装程序会创建开始菜单和可选的桌面快捷方式；如果缺少 Microsoft WebView2 Runtime，会联网安装微软运行时。

## 便携版

把 ZIP **全部解压**到一个文件夹，双击其中的 `Muse.exe`。不要直接从压缩包内运行，不要只移动 EXE，旁边的 DLL 必须保留。如果提示缺少 WebView2 Runtime，先运行同目录的 `MicrosoftEdgeWebview2Setup.exe`，再打开 Muse。

## 登录与托盘

首次在软件内登录自己的 Muse 账户，软件会保存本机的登录会话。它不共享 Edge 浏览器已有的登录，也不包含发布者的账号或聊天记录。

点关闭按钮会进入托盘；双击托盘图标或再次打开程序可恢复。彻底退出请右键托盘图标选择“退出 Muse”。任务栏向上箭头内可能收纳了托盘图标。

软件自动记住窗口大小、位置和最大化状态。应用数据位于 `%USERPROFILE%\AppData\Local\MuseDesktop`。卸载程序时会保留此数据，方便重新安装后继续使用。

下载页的 `SHA256SUMS.txt` 可用于核对发行文件是否完整。
