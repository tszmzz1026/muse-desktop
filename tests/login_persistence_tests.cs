using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

internal static class LoginPersistenceTests
{
    private static void check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string phase = args[0];
        string data_path = args[1];
        string report_path = args[2];
        string settings_path = Path.Combine(Path.GetDirectoryName(report_path), "window.json");
        Application.EnableVisualStyles();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using (EventWaitHandle activate_event = new EventWaitHandle(false, EventResetMode.AutoReset))
        using (EventWaitHandle exit_event = new EventWaitHandle(false, EventResetMode.AutoReset))
        using (MuseWindow window = new MuseWindow(delegate(Control host)
        {
            return new WebAppView(host, data_path, "about:blank");
        }, activate_event, exit_event, settings_path))
        using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer())
        {
            bool busy = false;
            bool completed = false;
            Exception failure = null;
            DateTime changed_at_utc = DateTime.MinValue;
            Stopwatch watch = Stopwatch.StartNew();
            timer.Interval = 100;
            timer.Tick += async delegate
            {
                if (busy) { return; }
                busy = true;
                try
                {
                    WebAppView browser = (WebAppView)window.app_view;
                    if (!browser.is_ready)
                    {
                        check(watch.Elapsed.TotalSeconds < 25 && browser.is_starting, "Browser initialization failed: " + browser.error_message);
                        busy = false;
                        return;
                    }
                    CoreWebView2CookieManager manager = browser.active_view.CoreWebView2.CookieManager;
                    if (phase == "write" || phase == "write-abrupt")
                    {
                        if (phase == "write-abrupt" && changed_at_utc == DateTime.MinValue)
                        {
                            string ready = await browser.active_view.CoreWebView2.ExecuteScriptAsync("document.readyState === 'complete'");
                            if (ready != "true") { busy = false; return; }
                            await browser.save_state_async();
                        }
                        string session_value = phase == "write" ? "fixture-session" : "fixture-refreshed";
                        if (changed_at_utc != DateTime.MinValue)
                        {
                            string cache_path = Path.Combine(data_path, "session-cookies.dat");
                            if (!File.Exists(cache_path) || File.GetLastWriteTimeUtc(cache_path) < changed_at_utc)
                            {
                                check(watch.Elapsed.TotalSeconds < 20, "Periodic session checkpoint did not run.");
                                busy = false;
                                return;
                            }
                            using (StreamWriter writer = new StreamWriter(report_path, false)) { writer.WriteLine("PASS: background checkpoint before abrupt process exit"); }
                            Environment.Exit(0);
                        }
                        CoreWebView2Cookie session = manager.CreateCookie("muse_test_session", session_value, ".muse.ai", "/");
                        session.IsHttpOnly = true;
                        session.IsSecure = true;
                        session.SameSite = CoreWebView2CookieSameSiteKind.Strict;
                        check(session.IsSession, "The fixture must use a session cookie.");
                        manager.AddOrUpdateCookie(session);
                        CoreWebView2Cookie persistent = manager.CreateCookie("muse_test_persistent", "fixture-persistent", ".muse.ai", "/");
                        persistent.Expires = DateTime.UtcNow.AddDays(1);
                        manager.AddOrUpdateCookie(persistent);
                        var written = await manager.GetCookiesAsync("https://muse.ai/");
                        check(written.Count == 2, "Fixture cookies were not set.");
                        if (phase == "write-abrupt")
                        {
                            changed_at_utc = DateTime.UtcNow;
                            busy = false;
                            return;
                        }
                    }
                    else if (phase == "logout")
                    {
                        var before_logout = await manager.GetCookiesAsync("https://muse.ai/");
                        foreach (CoreWebView2Cookie cookie in before_logout.Where(cookie => cookie.Name == "muse_test_session")) { manager.DeleteCookie(cookie); }
                        var after_logout = await manager.GetCookiesAsync("https://muse.ai/");
                        if (after_logout.Any(cookie => cookie.Name == "muse_test_session"))
                        {
                            check(watch.Elapsed.TotalSeconds < 20, "The logout fixture did not remove its session cookie.");
                            busy = false;
                            return;
                        }
                    }
                    else
                    {
                        var cookies = await manager.GetCookiesAsync("https://muse.ai/");
                        check(cookies.Any(cookie => cookie.Name == "muse_test_persistent"), "Persistent-cookie control was lost; profile data is not durable.");
                        CoreWebView2Cookie session = cookies.FirstOrDefault(cookie => cookie.Name == "muse_test_session");
                        if (phase == "read-logout" || phase == "read-corrupt")
                        {
                            check(session == null, "An explicitly removed login cookie was restored after logout.");
                        }
                        else
                        {
                            check(session != null, "Session login cookie is missing after a complete process restart.");
                            string expected_value = phase == "read-abrupt" ? "fixture-refreshed" : "fixture-session";
                            check(session.Value == expected_value, "Restored session did not contain the latest value.");
                            check(session.IsSession && session.IsSecure && session.IsHttpOnly && session.SameSite == CoreWebView2CookieSameSiteKind.Strict,
                                "Session cookie attributes changed during restore.");
                        }
                    }
                    completed = true;
                    timer.Stop();
                    window.exit_application();
                }
                catch (Exception error)
                {
                    failure = error;
                    timer.Stop();
                    window.exit_application();
                }
            };
            timer.Start();
            try { Application.Run(window); }
            catch (Exception error) { failure = error; window.Dispose(); }
            using (StreamWriter writer = new StreamWriter(report_path, false))
            {
                if (failure != null) { writer.WriteLine(failure.ToString()); return 1; }
                if (!completed) { writer.WriteLine("Persistence test did not complete."); return 1; }
                writer.WriteLine("PASS: " + phase);
            }
            return 0;
        }
    }
}
