"use strict";

const path = require("node:path");
const { app, session, safeStorage, Menu, dialog } = require("electron");
const { MuseClient } = require("./client.cjs");
const { SessionStore } = require("./session_store.cjs");

app.setName("Muse Desktop");
app.enableSandbox();
app.setPath("userData", path.join(app.getPath("appData"), "MuseDesktop"));

let client;
let store;
let quitting = false;

async function quit() {
    if (quitting) {
        return;
    }
    quitting = true;
    client?.save_state();
    await store?.stop();
    client?.dispose();
    app.quit();
}

if (!app.requestSingleInstanceLock()) {
    app.quit();
} else {
    app.on("second-instance", () => client?.restore());
    app.on("activate", () => client?.restore());
    app.on("before-quit", (event) => {
        if (!quitting) {
            event.preventDefault();
            void quit();
        }
    });
    app.on("window-all-closed", () => { if (!quitting) { void quit(); } });
    process.on("SIGTERM", () => void quit());
    process.on("SIGINT", () => void quit());
    app.whenReady().then(async () => {
        Menu.setApplicationMenu(null);
        const browser_session = session.fromPartition("persist:muse");
        browser_session.setPermissionRequestHandler((_contents, _permission, callback) => callback(false));
        browser_session.setPermissionCheckHandler(() => false);
        store = new SessionStore(browser_session, safeStorage, path.join(app.getPath("userData"), "session-cookies.dat"));
        await store.restore();
        store.start();
        client = new MuseClient({ browser_session, state_path: path.join(app.getPath("userData"), "window_state.json"), on_exit: quit });
        await client.open();
    }).catch(() => {
        dialog.showErrorBox("Muse Desktop", "无法启动 Muse，请检查用户数据目录权限和系统图形环境。\nUnable to start Muse. Check the user data directory and desktop environment.");
        void quit();
    });
}
