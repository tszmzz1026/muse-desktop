"use strict";

const fs = require("node:fs");
const path = require("node:path");

function write_private(file_path, data) {
    fs.mkdirSync(path.dirname(file_path), { recursive: true, mode: 0o700 });
    const temporary_path = `${file_path}.tmp`;
    const descriptor = fs.openSync(temporary_path, "w", 0o600);
    try {
        fs.writeFileSync(descriptor, data);
        fs.fsyncSync(descriptor);
    } finally {
        fs.closeSync(descriptor);
    }
    fs.renameSync(temporary_path, file_path);
}

function read_state(file_path) {
    try {
        const saved = JSON.parse(fs.readFileSync(file_path, "utf8"));
        return saved && typeof saved === "object" && !Array.isArray(saved) ? saved : {};
    } catch {
        return {};
    }
}

function fit_bounds(saved, displays) {
    const area = displays.find(({ workArea }) => Number.isFinite(saved.x) && Number.isFinite(saved.y)
        && saved.x < workArea.x + workArea.width && saved.x + saved.width > workArea.x
        && saved.y < workArea.y + workArea.height && saved.y + saved.height > workArea.y)?.workArea
        || displays[0].workArea;
    const width = Math.min(area.width, Math.max(640, Number.isFinite(saved.width) ? Math.round(saved.width) : 1200));
    const height = Math.min(area.height, Math.max(480, Number.isFinite(saved.height) ? Math.round(saved.height) : 820));
    const x = Number.isFinite(saved.x) ? Math.round(saved.x) : area.x + Math.floor((area.width - width) / 2);
    const y = Number.isFinite(saved.y) ? Math.round(saved.y) : area.y + Math.floor((area.height - height) / 2);
    return {
        x: Math.max(area.x, Math.min(x, area.x + area.width - width)),
        y: Math.max(area.y, Math.min(y, area.y + area.height - height)),
        width,
        height
    };
}

function is_muse_domain(domain) {
    const hostname = String(domain).replace(/^\./, "").toLowerCase();
    return hostname === "muse.ai" || hostname.endsWith(".muse.ai");
}

function cookie_details(cookie) {
    if (!is_muse_domain(cookie.domain) || cookie.session !== true
        || typeof cookie.name !== "string" || typeof cookie.value !== "string"
        || typeof cookie.path !== "string" || !cookie.path.startsWith("/")) {
        throw new Error("Invalid Muse session cookie");
    }
    const result = {
        url: `https://${cookie.domain.replace(/^\./, "")}${cookie.path}`,
        name: cookie.name,
        value: cookie.value,
        path: cookie.path,
        secure: cookie.secure === true,
        httpOnly: cookie.httpOnly === true,
        sameSite: cookie.sameSite
    };
    if (!cookie.hostOnly) {
        result.domain = cookie.domain;
    }
    return result;
}

function can_navigate(url) {
    try {
        return url === "about:blank" || new URL(url).protocol === "https:";
    } catch {
        return false;
    }
}

module.exports = { write_private, read_state, fit_bounds, is_muse_domain, cookie_details, can_navigate };
