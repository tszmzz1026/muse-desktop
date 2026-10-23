"use strict";

const fs = require("node:fs");
const { write_private, is_muse_domain, cookie_details } = require("./state.cjs");

const MAX_IDLE_AGE = 30 * 24 * 60 * 60 * 1000;

class SessionStore {
    constructor(browser_session, safe_storage, file_path) {
        this.browser_session = browser_session;
        this.safe_storage = safe_storage;
        this.file_path = file_path;
        this.pending = Promise.resolve();
        this.restoring = false;
        this.last_error = false;
        this.on_change = (_event, cookie) => {
            if (!this.restoring && is_muse_domain(cookie.domain)) {
                clearTimeout(this.debounce);
                this.debounce = setTimeout(() => this.save(), 150);
            }
        };
    }

    get encryption_available() {
        return this.safe_storage.isEncryptionAvailable()
            && this.safe_storage.getSelectedStorageBackend() !== "basic_text";
    }

    async restore() {
        if (!this.encryption_available) {
            return;
        }
        this.restoring = true;
        try {
            if (!fs.existsSync(this.file_path)) {
                return;
            }
            const saved = JSON.parse(this.safe_storage.decryptString(fs.readFileSync(this.file_path)));
            if (saved.version !== 1 || !Number.isFinite(saved.saved_at)
                || saved.saved_at > Date.now() + 60000 || Date.now() - saved.saved_at > MAX_IDLE_AGE
                || !Array.isArray(saved.cookies)) {
                return;
            }
            const current = await this.browser_session.cookies.get({});
            for (const cookie of saved.cookies) {
                const details = cookie_details(cookie);
                if (!current.some((item) => item.name === cookie.name && item.domain === cookie.domain && item.path === cookie.path)) {
                    await this.browser_session.cookies.set(details);
                }
            }
        } catch {
            this.last_error = true;
        } finally {
            this.restoring = false;
        }
    }

    start() {
        this.browser_session.cookies.on("changed", this.on_change);
        this.timer = setInterval(() => this.save(), 5000);
    }

    save() {
        this.pending = this.pending.then(async () => {
            const cookies = (await this.browser_session.cookies.get({})).filter((cookie) => cookie.session && is_muse_domain(cookie.domain));
            if (cookies.length === 0) {
                fs.rmSync(this.file_path, { force: true });
                this.last_cookies = undefined;
            } else if (this.encryption_available) {
                const serialized_cookies = JSON.stringify(cookies);
                if (serialized_cookies !== this.last_cookies || Date.now() - this.last_save >= 86400000) {
                    const serialized = JSON.stringify({ version: 1, saved_at: Date.now(), cookies });
                    write_private(this.file_path, this.safe_storage.encryptString(serialized));
                    this.last_cookies = serialized_cookies;
                    this.last_save = Date.now();
                }
            }
            await this.browser_session.cookies.flushStore();
            this.browser_session.flushStorageData();
            this.last_error = false;
        }).catch(() => {
            this.last_error = true;
        });
        return this.pending;
    }

    async stop() {
        clearTimeout(this.debounce);
        clearInterval(this.timer);
        this.browser_session.cookies.removeListener("changed", this.on_change);
        await this.save();
    }
}

module.exports = { SessionStore };
