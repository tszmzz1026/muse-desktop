"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { app, session, safeStorage } = require("electron");
const { SessionStore } = require("../src/session_store.cjs");

const [phase, directory] = process.argv.slice(2);
app.setName("Muse Desktop Tests");
app.setPath("userData", directory);
app.enableSandbox();

app.whenReady().then(async () => {
    const browser_session = session.fromPartition("persist:roundtrip");
    const snapshot_path = path.join(directory, "session-cookies.dat");
    const store = new SessionStore(browser_session, safeStorage, snapshot_path);
    assert.equal(store.encryption_available, true, "A working GNOME Keyring or KWallet is required for this test");
    if (phase === "corrupt") {
        fs.writeFileSync(snapshot_path, "invalid snapshot");
    }
    await store.restore();
    store.start();
    const get_session = () => browser_session.cookies.get({ name: "muse_session_fixture" });
    if (phase === "write" || phase === "abrupt") {
        await browser_session.cookies.set({ url: "https://muse.ai/", name: "muse_session_fixture",
            value: phase === "write" ? "private-fixture-one" : "private-fixture-two", secure: true, httpOnly: true, sameSite: "strict" });
        await browser_session.cookies.set({ url: "https://muse.ai/", name: "persistent_fixture", value: "persistent",
            expirationDate: Math.floor(Date.now() / 1000) + 3600, secure: true });
        await browser_session.cookies.set({ url: "https://example.com/", name: "unrelated_fixture", value: "excluded" });
        if (phase === "write") {
            await store.stop();
        } else {
            await new Promise((resolve) => setTimeout(resolve, 700));
            await store.pending;
        }
        const encrypted = fs.readFileSync(snapshot_path);
        assert.equal(encrypted.includes(Buffer.from("private-fixture")), false);
        assert.equal(fs.statSync(snapshot_path).mode & 0o777, 0o600);
        const payload = JSON.parse(safeStorage.decryptString(encrypted));
        assert.equal(payload.cookies.length, 1);
        assert.equal(payload.cookies[0].name, "muse_session_fixture");
        if (phase === "abrupt") {
            app.exit(0);
            return;
        }
    } else if (phase === "read" || phase === "read-abrupt") {
        const cookies = await get_session();
        assert.equal(cookies.length, 1);
        const cookie = cookies[0];
        assert.equal(cookie.value, phase === "read" ? "private-fixture-one" : "private-fixture-two");
        assert.equal(cookie.secure, true);
        assert.equal(cookie.httpOnly, true);
        assert.equal(cookie.sameSite, "strict");
        assert.equal(cookie.session, true);
        assert.equal((await browser_session.cookies.get({ name: "persistent_fixture" })).length, 1);
        await store.stop();
    } else if (phase === "logout") {
        await browser_session.cookies.remove("https://muse.ai/", "muse_session_fixture");
        await store.stop();
        assert.equal(fs.existsSync(snapshot_path), false);
    } else {
        assert.equal((await get_session()).length, 0);
        await store.stop();
    }
    console.log(`PASS session ${phase}`);
    app.quit();
}).catch((error) => {
    console.error(error);
    app.exit(1);
});
