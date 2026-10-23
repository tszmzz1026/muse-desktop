"use strict";

const path = require("node:path");
const { execFile } = require("node:child_process");
const { promisify } = require("node:util");
const { BrowserWindow, WebContentsView, Menu, Tray, nativeImage, screen } = require("electron");
const { read_state, write_private, fit_bounds, can_navigate } = require("./state.cjs");

const exec_file = promisify(execFile);
const ICON_PATH = path.join(__dirname, "../assets/muse.png");

async function has_tray_host() {
    try {
        const { stdout } = await exec_file("gdbus", ["call", "--session", "--dest", "org.freedesktop.DBus",
            "--object-path", "/org/freedesktop/DBus", "--method", "org.freedesktop.DBus.NameHasOwner",
            "org.kde.StatusNotifierWatcher"], { timeout: 1500 });
        return stdout.includes("true");
    } catch {
        return false;
    }
}

class MuseClient {
    constructor({ browser_session, state_path, on_exit, tray_available = has_tray_host }) {
        this.browser_session = browser_session;
        this.state_path = state_path;
        this.on_exit = on_exit;
        this.tray_available = tray_available;
        this.views = [];
        this.exiting = false;
        this.preferences = { session: browser_session, sandbox: true, contextIsolation: true,
            nodeIntegration: false, webSecurity: true, allowRunningInsecureContent: false, navigateOnDragDrop: false };
    }

    async open(start_url = "https://muse.ai/") {
        const saved = read_state(this.state_path);
        this.window = new BrowserWindow({
            ...fit_bounds(saved, screen.getAllDisplays()),
            title: "Muse Desktop", icon: ICON_PATH, show: false,
            webPreferences: { sandbox: true, nodeIntegration: false, contextIsolation: true }
        });
        this.window.setMenuBarVisibility(false);
        this.window.on("resize", () => { this.layout(); this.schedule_state(); });
        this.window.on("move", () => this.schedule_state());
        this.window.on("maximize", () => this.schedule_state());
        this.window.on("unmaximize", () => this.schedule_state());
        this.window.on("close", (event) => {
            if (!this.exiting) {
                event.preventDefault();
                void this.close_requested();
            }
        });
        const view = this.add_view(this.preferences);
        view.webContents.on("did-fail-load", (_event, code, _description, _url, main_frame) => {
            if (main_frame && code !== -3) {
                void view.webContents.loadFile(path.join(__dirname, "error.html"));
            }
        });
        await this.create_tray();
        if (saved.maximized === true) {
            this.window.maximize();
        }
        this.window.show();
        await view.webContents.loadURL(start_url).catch(() => {});
        return view;
    }

    add_view(preferences, existing_contents) {
        const options = { webPreferences: { ...preferences, ...this.preferences } };
        if (existing_contents) {
            options.webContents = existing_contents;
        }
        const view = new WebContentsView(options);
        const previous = this.views.at(-1);
        if (previous) {
            this.window.contentView.removeChildView(previous);
        }
        this.views.push(view);
        this.window.contentView.addChildView(view);
        this.layout();
        const contents = view.webContents;
        contents.setWindowOpenHandler(({ url }) => {
            if (!can_navigate(url)) {
                return { action: "deny" };
            }
            return {
                action: "allow",
                createWindow: (options) => this.add_view(options.webPreferences, options.webContents).webContents
            };
        });
        contents.on("will-navigate", (event, url) => {
            if (!can_navigate(url)) {
                event.preventDefault();
            }
        });
        contents.on("will-redirect", (event, url) => {
            if (!can_navigate(url)) {
                event.preventDefault();
            }
        });
        contents.on("will-attach-webview", (event) => event.preventDefault());
        contents.on("page-title-updated", (_event, title) => {
            if (this.views.at(-1) === view) {
                this.window.setTitle(`${title || "Muse"} — Muse Desktop`);
            }
        });
        contents.on("destroyed", () => this.remove_view(view));
        contents.on("context-menu", () => {
            Menu.buildFromTemplate([
                { label: "返回上一页 / Back", enabled: contents.navigationHistory.canGoBack(), click: () => contents.navigationHistory.goBack() },
                { label: "重新加载 / Reload", click: () => contents.reload() },
                { type: "separator" }, { role: "cut" }, { role: "copy" }, { role: "paste" }, { role: "selectAll" },
                { type: "separator" },
                { label: "返回主页面 / Close login page", enabled: this.views.length > 1, click: () => contents.close() }
            ]).popup({ window: this.window });
        });
        return view;
    }

    remove_view(view) {
        const index = this.views.indexOf(view);
        if (index < 0 || this.exiting || this.window.isDestroyed()) {
            return;
        }
        const was_active = this.views.at(-1) === view;
        this.window.contentView.removeChildView(view);
        this.views.splice(index, 1);
        if (was_active && this.views.length > 0) {
            const previous = this.views.at(-1);
            this.window.contentView.addChildView(previous);
            this.layout();
            previous.webContents.focus();
        } else if (this.views.length === 0) {
            this.on_exit();
        }
    }

    layout() {
        const [width, height] = this.window.getContentSize();
        for (const view of this.views) {
            view.setBounds({ x: 0, y: 0, width, height });
        }
    }

    schedule_state() {
        clearTimeout(this.state_timer);
        this.state_timer = setTimeout(() => this.save_state(), 250);
    }

    save_state() {
        if (!this.window || this.window.isDestroyed()) {
            return;
        }
        try {
            write_private(this.state_path, JSON.stringify({ ...this.window.getNormalBounds(), maximized: this.window.isMaximized() }));
        } catch {
            // Keep the running window usable if the disk is full.
        }
    }

    restore() {
        if (this.window.isMinimized()) {
            this.window.restore();
        }
        this.window.show();
        this.window.focus();
    }

    async close_requested() {
        this.save_state();
        if (this.tray && !this.tray.isDestroyed() && await this.tray_available()) {
            this.window.hide();
        } else {
            this.on_exit();
        }
    }

    async create_tray() {
        if (!await this.tray_available()) {
            return;
        }
        this.tray = new Tray(nativeImage.createFromPath(ICON_PATH).resize({ width: 32, height: 32 }));
        this.tray.setToolTip("Muse Desktop");
        this.tray.setContextMenu(Menu.buildFromTemplate([
            { label: "打开 Muse / Show Muse", click: () => this.restore() },
            { type: "separator" },
            { label: "退出 Muse / Quit Muse", click: () => this.on_exit() }
        ]));
        this.tray.on("click", () => this.restore());
        this.tray.on("double-click", () => this.restore());
        this.tray_timer = setInterval(async () => {
            if (!this.exiting && !await this.tray_available() && !this.window.isVisible()) {
                this.restore();
            }
        }, 5000);
    }

    dispose() {
        this.save_state();
        this.exiting = true;
        clearTimeout(this.state_timer);
        clearInterval(this.tray_timer);
        this.tray?.destroy();
        for (const view of this.views) {
            if (!view.webContents.isDestroyed()) {
                view.webContents.close({ waitForBeforeUnload: false });
            }
        }
        this.window.destroy();
    }
}

module.exports = { MuseClient, has_tray_host };
