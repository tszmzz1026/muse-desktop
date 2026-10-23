# Muse 桌面客户端

**[下载 Windows / Linux 客户端](https://github.com/tszmzz1026/muse-desktop/releases/latest)**

Muse.ai 非官方桌面客户端。Windows 使用微软 WebView2，Linux 使用 Electron，直接呈现 `https://muse.ai/`，只有一个主窗口和一层标题栏，页面铺满内容区。

支持 Windows 10 2004 及以上版本、Windows 11，64 位系统。下载 `Setup.exe` 后双击安装；安装程序会按需联网安装 WebView2 Runtime。便携版需要全部解压，再运行其中的 `Muse.exe`。[详细使用说明](docs/DOWNLOADS.md)。

Linux 提供 x86_64 的 `.deb` 和 `.AppImage`，以 Ubuntu 22.04 为构建与验证环境；Ubuntu、Debian、Linux Mint 等桌面系统可使用。ARM64 暂未提供。Ubuntu/Debian 推荐安装 `.deb`，会添加应用菜单入口并自动处理依赖。

```bash
sudo apt install ./Muse-Desktop-1.1.0-linux-amd64.deb
```

其他发行版可给 AppImage 添加执行权限后运行，需要图形桌面、GTK 3、NSS、GBM 和可用的用户命名空间。系统限制 AppImage 沙箱时，Ubuntu/Debian 请使用 `.deb`。

```bash
chmod +x Muse-Desktop-1.1.0-linux-x86_64.AppImage
./Muse-Desktop-1.1.0-linux-x86_64.AppImage
```

## 使用

- 点击主窗口的关闭按钮：隐藏到系统托盘，保留页面和未发送的输入。
- 双击托盘图标，或再次双击桌面快捷方式：恢复原窗口。
- 右键托盘图标选择 **退出 Muse**：彻底退出软件。
- 窗口大小、位置和最大化状态自动保存，下次启动恢复。
- 网站要求打开新的登录页面时，在同一主窗口内显示，登录完成后可由网站关闭该页面并返回原页面。

Windows 可能将托盘图标收纳在任务栏的向上箭头中。

Linux 托盘依赖桌面的 StatusNotifier/AppIndicator 支持。KDE Plasma 通常自带；GNOME 可启用 AppIndicator 扩展。若没有可用的托盘，关闭按钮会正常退出，避免窗口隐藏后无法找回；再次运行程序会恢复已有窗口。

## 登录

WebView2 使用自己的应用数据目录，不能直接共享 Edge 浏览器中现有的网站会话。首次在软件内使用与 Edge 相同的 Muse 账户登录，之后网站会话保存在本机，直到过期或退出登录。软件不会复制 Edge 的 Cookie、密码或令牌。

网站数据保存在 `%USERPROFILE%\AppData\Local\MuseDesktop\WebView2`，窗口配置保存在同级的 `window_state.json`。网站功能及聊天内容由 Muse 提供，需要联网。

v1.0.1 修复了会话 Cookie 在重启后丢失的问题：仅对 Muse 域名下的会话 Cookie 使用 Windows 当前用户加密保护保存，启动时恢复原来的安全属性，定期保存以应对关机；持久 Cookie 的有效期保持不变。主动退出登录会清除会话记录。升级后需要先登录一次；网站主动过期的会话仍需重新验证。

Linux 的数据保存在 `${XDG_CONFIG_HOME:-~/.config}/MuseDesktop`，与 Windows 数据独立。首次登录后，会话 Cookie 使用 GNOME Keyring / KWallet 加密保存，并保留网站原有安全属性。需要正常运行并解锁的系统密钥环；密钥环不可用时，不会创建额外的明文会话备份，重启后可能需要重新登录。主动注销会清除备份，网站使会话失效后仍需重新登录。

## 构建与验证

开发环境：Windows 10/11 x64、.NET Framework 4.6.2+、Microsoft Edge WebView2 Runtime。首次构建会从官方 NuGet 下载固定版本 `Microsoft.Web.WebView2 1.0.4258.31`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Integration
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test_login_persistence.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

集成检查使用独立测试目录与本地空白页面，验证页面铺满、保留草稿的托盘恢复、窗口尺寸记忆、同窗口登录弹页及退出清理。登录状态测试使用虚构 Cookie 验证跨进程重启、非正常退出、加密存储、主动注销与损坏记录恢复，不访问真实账户。

打包需要 Inno Setup 6.3 或更新版本，可通过 `-InstallerCompiler` 指定 `ISCC.exe`。GitHub Actions 分别构建 Windows 和 Linux 发行包。发布目录只包含程序文件、依赖和使用说明，不包含本机用户数据。

Linux 构建需要 Node.js 24 和 Linux x86_64 环境：

```bash
cd linux
npm ci
npm test
npm run dist
```

Linux CI 在 Xvfb + Openbox 下验证页面铺满、托盘与草稿恢复、同窗口 OAuth 页面与 opener 通信、尺寸恢复、无托盘时退出；在真实系统密钥环中验证会话跨进程恢复、突然退出、主动注销和损坏缓存。打包后还会安装 `.deb` 并启动 `.deb` / AppImage，验证单实例和关闭退出。测试使用隔离数据目录和虚构 Cookie，不访问真实账户。Linux 包自带 Chromium，不需要安装 Edge 或 WebView2。

`dist` 内的 `Muse.exe`、两个 `Microsoft.Web.WebView2.*.dll` 和 `WebView2Loader.dll` 必须放在一起。`Muse.exe --diagnose` 在程序旁写入 `diagnostics.json`，记录运行时版本、数据目录和窗口模式。

## 移除

从托盘右键退出软件后，可删除桌面快捷方式和程序文件夹。应用登录数据单独保留在上述数据目录；若不再使用，可自行删除该目录。
