# Muse Desktop v1.1.0

新增 Linux x86_64 客户端，直接在独立窗口内使用 Muse.ai。

- `Muse-Desktop-1.1.0-linux-amd64.deb`：Ubuntu / Debian / Linux Mint 推荐安装包，安装后可从应用菜单启动。
- `Muse-Desktop-1.1.0-linux-x86_64.AppImage`：便携运行，添加执行权限后打开。
- 单主窗口；登录弹页在同一窗口中切换，不产生第二个窗口。
- 支持托盘、单实例、保留未发送内容和恢复上次窗口大小。
- 使用 GNOME Keyring / KWallet 加密保存 Muse 会话 Cookie，跨进程恢复；主动退出账户会清除记录。需要可用的系统密钥环。
- 桌面没有 StatusNotifier/AppIndicator 托盘支持时，关闭按钮正常退出。GNOME 可启用 AppIndicator 扩展后使用托盘。

Linux 以 Ubuntu 22.04 x86_64 构建和验证，暂不包含 ARM64。AppImage 需要图形桌面和可用的用户命名空间；Ubuntu/Debian 推荐 `.deb`。Linux 包自带 Chromium，不需要 Edge/WebView2。

Windows 继续提供 v1.0.1 安装版与便携版，保留重启后的登录记录；Windows 程序未作变更。

验证：Linux 窗口/草稿/托盘/OAuth 集成测试，真实系统密钥环下的正常退出、突然退出、注销及损坏缓存恢复；发行包安装、启动与单实例检查。验证覆盖完整进程重启，未执行物理电脑重启。

自动验证：[Ubuntu 22.04 / Windows CI](https://github.com/tszmzz1026/muse-desktop/actions/runs/39924864494)。

软件非 Muse 官方产品。每位使用者登录自己的账户，发行包不包含任何个人登录数据。
