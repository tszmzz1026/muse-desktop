"use strict";

const { test } = require("node:test");
const assert = require("node:assert/strict");
const { fit_bounds, is_muse_domain, cookie_details, can_navigate } = require("../src/state.cjs");

test("window returns to a visible screen after monitor removal", () => {
    const displays = [{ workArea: { x: 0, y: 0, width: 1366, height: 728 } }];
    assert.deepEqual(fit_bounds({ x: 3000, y: 1000, width: 1900, height: 1000 }, displays),
        { x: 0, y: 0, width: 1366, height: 728 });
    assert.deepEqual(fit_bounds({ width: -1, height: NaN }, displays),
        { x: 363, y: 0, width: 640, height: 728 });
});

test("Muse domain restriction rejects suffix lookalikes", () => {
    for (const domain of ["muse.ai", ".muse.ai", "auth.muse.ai"]) {
        assert.equal(is_muse_domain(domain), true);
    }
    for (const domain of ["evil-muse.ai", "muse.ai.example.com", "example.com"]) {
        assert.equal(is_muse_domain(domain), false);
    }
});

test("session restoration retains security attributes without creating an expiry", () => {
    const details = cookie_details({ name: "session", value: "fixture", domain: "auth.muse.ai",
        hostOnly: true, session: true, path: "/auth", secure: true, httpOnly: true, sameSite: "strict" });
    assert.equal(details.url, "https://auth.muse.ai/auth");
    assert.equal(details.secure, true);
    assert.equal(details.httpOnly, true);
    assert.equal(details.sameSite, "strict");
    assert.equal("domain" in details, false);
    assert.equal("expirationDate" in details, false);
    assert.throws(() => cookie_details({ session: true, domain: "other.ai" }));
});

test("untrusted navigation cannot access local files or execute schemes", () => {
    assert.equal(can_navigate("https://accounts.google.com/"), true);
    assert.equal(can_navigate("about:blank"), true);
    for (const url of ["file:///etc/passwd", "javascript:alert(1)", "http://muse.ai", "data:text/html,test", "invalid"]) {
        assert.equal(can_navigate(url), false);
    }
});
