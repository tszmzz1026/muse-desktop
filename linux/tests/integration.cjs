"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { app, session, BrowserWindow, screen } = require("electron");
const { MuseClient } = require("../src/client.cjs");

const directory = fs.mkdtempSync(path.join(os.tmpdir(), "muse-ui-test-"));
process.on("uncaughtException", (error) => { console.error(error); app.exit(1); });
app.on("window-all-closed", () => {});
app.setPath("userData", directory);
app.enableSandbox();

async function wait_until(predicate) {
    const deadline = Date.now() + 7000;
    while (!predicate()) {
        assert.ok(Date.now() < deadline, "Timed out waiting for window state");
        await new Promise((resolve) => setTimeout(resolve, 30));
    }
}

app.whenReady().then(async () => {
    let exit_requested = false;
    let tray_present = true;
    const options = { browser_session: session.fromPartition("persist:ui-test"),
        state_path: path.join(directory, "window_state.json"),
        tray_available: async () => tray_present, on_exit: () => { exit_requested = true; } };
    let client = new MuseClient(options);
    const view = await client.open("about:blank");
    assert.equal(BrowserWindow.getAllWindows().length, 1);
    assert.deepEqual(view.getBounds(), { x: 0, y: 0, width: client.window.getContentSize()[0], height: client.window.getContentSize()[1] });
    assert.equal(view.webContents.getLastWebPreferences().sandbox, true);
    assert.equal(view.webContents.getLastWebPreferences().nodeIntegration, false);
    assert.equal(await view.webContents.executeJavaScript("typeof require"), "undefined");
    await view.webContents.executeJavaScript("document.body.innerHTML = '<input id=message>'; document.querySelector('input').value = 'unsent draft'; void 0");
    client.window.close();
    await wait_until(() => !client.window.isVisible());
    assert.equal(exit_requested, false);
    client.restore();
    assert.equal(await view.webContents.executeJavaScript("document.querySelector('input').value"), "unsent draft");
    await view.webContents.executeJavaScript("window.received = false; window.addEventListener('message', () => { window.received = true; }); void window.open('about:blank', 'oauth')");
    await wait_until(() => client.views.length === 2);
    assert.equal(BrowserWindow.getAllWindows().length, 1);
    const popup = client.views.at(-1);
    await popup.webContents.executeJavaScript("window.opener.postMessage('auth-done', '*'); setTimeout(() => window.close(), 30); void 0");
    await wait_until(() => client.views.length === 1);
    assert.equal(await view.webContents.executeJavaScript("window.received"), true);
    assert.equal(await view.webContents.executeJavaScript("document.querySelector('input').value"), "unsent draft");
    const area = screen.getPrimaryDisplay().workArea;
    client.window.setBounds({ x: area.x + 80, y: area.y + 80, width: 900, height: 650 });
    await new Promise((resolve) => setTimeout(resolve, 400));
    const previous = client.window.getNormalBounds();
    client.save_state();
    client.dispose();
    client = new MuseClient(options);
    await client.open("about:blank");
    await new Promise((resolve) => setTimeout(resolve, 400));
    const restored = client.window.getNormalBounds();
    for (const dimension of ["x", "y", "width", "height"]) {
        assert.ok(Math.abs(restored[dimension] - previous[dimension]) <= (process.platform === "win32" ? 2 : 0), `${dimension} was not restored: ${JSON.stringify({ previous, restored })}`);
    }
    client.window.maximize();
    await wait_until(() => client.window.isMaximized());
    client.save_state();
    client.dispose();
    client = new MuseClient(options);
    await client.open("about:blank");
    await wait_until(() => client.window.isMaximized());
    tray_present = false;
    client.window.close();
    await wait_until(() => exit_requested);
    assert.equal(client.window.isVisible(), true);
    client.dispose();
    console.log("PASS single window, sandbox, tray draft, OAuth opener/close, window bounds, missing tray fallback");
    app.quit();
}).catch((error) => {
    console.error(error);
    app.exit(1);
});
