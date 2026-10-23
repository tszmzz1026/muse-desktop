# Muse Desktop 使用说明

Muse.ai 非官方 Windows / Linux 桌面客户端。Windows 支持 Windows 10 2004 及以上版本、Windows 11；Linux 提供 x86_64 版本。网站功能需要联网及你自己的 Muse 账户。

## Linux

Ubuntu / Debian / Linux Mint 推荐下载 `.deb`，在下载目录执行：

```bash
sudo apt install ./Muse-Desktop-1.1.0-linux-amd64.deb
```

然后从应用菜单打开 **Muse Desktop**，或运行 `muse-desktop`。其他发行版可运行 AppImage：

```bash
chmod +x Muse-Desktop-1.1.0-linux-x64.AppImage
./Muse-Desktop-1.1.0-linux-x64.AppImage
```

需要图形桌面及 GTK 3、NSS、GBM 等常规桌面库，AppImage 需要可用的用户命名空间。Linux 版本以 Ubuntu 22.04 构建和验证。Ubuntu/Debian 遇到 AppImage 的沙箱限制时，请使用 `.deb`。

关闭进入托盘需要桌面支持 StatusNotifier/AppIndicator；GNOME 可启用 AppIndicator 扩展。没有可用托盘时，关闭会退出程序。窗口大小、位置和最大化状态会保存。

登录状态使用 GNOME Keyring / KWallet 保存，需正常运行并解锁的系统密钥环；密钥环不可用时，重启后可能需要重新登录。应用数据位于 `${XDG_CONFIG_HOME:-~/.config}/MuseDesktop`，不共享 Edge 或其他浏览器的账户，也不包含发布者的数据。卸载 `.deb` 可执行 `sudo apt remove muse-desktop`，用户数据会保留。

## 推荐：安装版

双击 `Muse-Desktop-1.0.1-Setup.exe`，按步骤安装。安装程序会创建开始菜单和可选的桌面快捷方式；如果缺少 Microsoft WebView2 Runtime，会联网安装微软运行时。

## 便携版

把 ZIP **全部解压**到一个文件夹，双击其中的 `Muse.exe`。不要直接从压缩包内运行，不要只移动 EXE，旁边的 DLL 必须保留。如果提示缺少 WebView2 Runtime，先运行同目录的 `MicrosoftEdgeWebview2Setup.exe`，再打开 Muse。

## 登录与托盘

首次在软件内登录自己的 Muse 账户，软件会保存本机的登录会话。它不共享 Edge 浏览器已有的登录，也不包含发布者的账号或聊天记录。

从 v1.0.0 升级后，请登录一次以建立新的会话保存记录。会话 Cookie 使用 Windows 当前用户的加密保护保存，定期写入并在启动时恢复；主动退出登录会清除记录。网站使会话失效时，仍需按网站要求登录。

点关闭按钮会进入托盘；双击托盘图标或再次打开程序可恢复。彻底退出请右键托盘图标选择“退出 Muse”。任务栏向上箭头内可能收纳了托盘图标。

软件自动记住窗口大小、位置和最大化状态。应用数据位于 `%USERPROFILE%\AppData\Local\MuseDesktop`。卸载程序时会保留此数据，方便重新安装后继续使用。

下载页的 `SHA256SUMS.txt` / `SHA256SUMS-linux.txt` 可用于核对发行文件是否完整。
