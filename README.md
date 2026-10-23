# Muse 桌面客户端

**[下载 Windows 安装版 / 便携版](https://github.com/tszmzz1026/muse-desktop/releases/latest)**

Muse.ai 非官方 Windows 桌面客户端。软件使用微软 WebView2 直接呈现 `https://muse.ai/`，只有一个主窗口和一层标题栏，页面铺满内容区。

支持 Windows 10 2004 及以上版本、Windows 11，64 位系统。下载 `Setup.exe` 后双击安装；安装程序会按需联网安装 WebView2 Runtime。便携版需要全部解压，再运行其中的 `Muse.exe`。[详细使用说明](docs/DOWNLOADS.md)。

## 使用

- 点击主窗口的关闭按钮：隐藏到系统托盘，保留页面和未发送的输入。
- 双击托盘图标，或再次双击桌面快捷方式：恢复原窗口。
- 右键托盘图标选择 **退出 Muse**：彻底退出软件。
- 窗口大小、位置和最大化状态自动保存，下次启动恢复。
- 网站要求打开新的登录页面时，在同一主窗口内显示，登录完成后可由网站关闭该页面并返回原页面。

Windows 可能将托盘图标收纳在任务栏的向上箭头中。

## 登录

WebView2 使用自己的应用数据目录，不能直接共享 Edge 浏览器中现有的网站会话。首次在软件内使用与 Edge 相同的 Muse 账户登录，之后网站会话保存在本机，直到过期或退出登录。软件不会复制 Edge 的 Cookie、密码或令牌。

网站数据保存在 `%USERPROFILE%\AppData\Local\MuseDesktop\WebView2`，窗口配置保存在同级的 `window_state.json`。网站功能及聊天内容由 Muse 提供，需要联网。

v1.0.1 修复了会话 Cookie 在重启后丢失的问题：仅对 Muse 域名下的会话 Cookie 使用 Windows 当前用户加密保护保存，启动时恢复原来的安全属性，定期保存以应对关机；持久 Cookie 的有效期保持不变。主动退出登录会清除会话记录。升级后需要先登录一次；网站主动过期的会话仍需重新验证。

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

打包需要 Inno Setup 6.3 或更新版本，可通过 `-InstallerCompiler` 指定 `ISCC.exe`。GitHub Actions 会构建 Windows 发行包。发布目录只包含指定的程序文件、依赖和使用说明，不包含本机用户数据。

`dist` 内的 `Muse.exe`、两个 `Microsoft.Web.WebView2.*.dll` 和 `WebView2Loader.dll` 必须放在一起。`Muse.exe --diagnose` 在程序旁写入 `diagnostics.json`，记录运行时版本、数据目录和窗口模式。

## 移除

从托盘右键退出软件后，可删除桌面快捷方式和程序文件夹。应用登录数据单独保留在上述数据目录；若不再使用，可自行删除该目录。
