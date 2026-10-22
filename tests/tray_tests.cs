using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal sealed class FakeAppView : IAppView
{
    internal int start_count;
    internal bool visible;
    internal bool closed;
    public bool is_ready { get; private set; }
    public bool is_starting { get { return false; } }
    public string error_message { get { return ""; } }
    public void start() { start_count++; is_ready = true; }
    public void poll() { }
    public void show(Rectangle bounds, bool activate) { visible = true; }
    public void hide() { visible = false; }
    public void close() { closed = true; visible = false; is_ready = false; }
    public void Dispose() { close(); }
}

internal static class MuseTests
{
    private static void check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }

    private static void check_browser_layout(MuseWindow window, WebAppView browser)
    {
        check(browser.active_view.Parent == window, "WebView is not a direct control of the application.");
        check(browser.active_view.Bounds == window.ClientRectangle, "Website does not fill the client area.");
        check(Application.OpenForms.Count == 1, "More than one application window is open.");
    }

    [STAThread]
    private static int Main(string[] args)
    {
        bool integration = args.Length == 1 && args[0] == "--integration";
        string output_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, integration ? "integration-test.txt" : "tray-test.txt");
        string test_id = Guid.NewGuid().ToString("N");
        string settings_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-window-state-" + test_id + ".json");
        string data_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-webview", test_id);
        Application.EnableVisualStyles();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using (EventWaitHandle activate_event = new EventWaitHandle(false, EventResetMode.AutoReset))
        using (EventWaitHandle exit_event = new EventWaitHandle(false, EventResetMode.AutoReset))
        using (MuseWindow window = new MuseWindow(delegate(Control host)
        {
            if (integration) { return new WebAppView(host, data_path, "about:blank"); }
            return new FakeAppView();
        }, activate_event, exit_event, settings_path))
        using (System.Windows.Forms.Timer test_timer = new System.Windows.Forms.Timer())
        {
            int stage = 0;
            bool busy = false;
            bool fixture_requested = false;
            Exception failure = null;
            object original_view = null;
            Stopwatch watch = Stopwatch.StartNew();
            test_timer.Interval = 300;
            test_timer.Tick += async delegate
            {
                if (busy) { return; }
                busy = true;
                try
                {
                    if (watch.Elapsed.TotalSeconds > 40) { throw new TimeoutException("WebView2 lifecycle test timed out."); }
                    WebAppView browser = window.app_view as WebAppView;
                    if (stage == 0)
                    {
                        if (!window.app_view.is_ready)
                        {
                            check(window.app_view.is_starting, window.app_view.error_message);
                            return;
                        }
                        check(window.Visible && window.tray_visible, "Initial application or tray icon missing.");
                        original_view = integration ? (object)browser.active_view : window.app_view;
                        if (integration)
                        {
                            check_browser_layout(window, browser);
                            if (!fixture_requested)
                            {
                                fixture_requested = true;
                                browser.active_view.CoreWebView2.NavigateToString("<!doctype html><html><body><input id=message value=draft><script>window.muse_test_marker='preserved';window.muse_root=true;</script></body></html>");
                                return;
                            }
                            string fixture_ready = await browser.active_view.CoreWebView2.ExecuteScriptAsync("window.muse_root === true");
                            if (fixture_ready != "true") { return; }
                        }
                        window.Close();
                        check(!window.Visible && !window.IsDisposed && window.tray_visible && window.app_view.is_ready,
                            "Close did not retain the application in the tray.");
                        activate_event.Set();
                        stage = 1;
                    }
                    else if (stage == 1)
                    {
                        if (!window.Visible) { return; }
                        check(window.app_view.is_ready, "Activation did not retain the page.");
                        if (integration)
                        {
                            check(Object.ReferenceEquals(original_view, browser.active_view), "Restore recreated the browser.");
                            string retained = await browser.active_view.CoreWebView2.ExecuteScriptAsync("window.muse_test_marker + ':' + document.getElementById('message').value");
                            check(retained == "\"preserved:draft\"", "Restore lost the page or unsent draft.");
                        }
                        window.Bounds = new Rectangle(Screen.PrimaryScreen.WorkingArea.Left + 40, Screen.PrimaryScreen.WorkingArea.Top + 40, 920, 650);
                        if (integration) { check_browser_layout(window, browser); }
                        window.Close();
                        using (MuseWindow reopened = new MuseWindow(delegate(Control host) { return new FakeAppView(); }, activate_event, exit_event, settings_path))
                        {
                            check(reopened.Bounds == window.Bounds, "Window size and position were not remembered.");
                            reopened.exit_application();
                        }
                        window.restore_window();
                        window.WindowState = FormWindowState.Maximized;
                        window.Close();
                        using (MuseWindow reopened = new MuseWindow(delegate(Control host) { return new FakeAppView(); }, activate_event, exit_event, settings_path))
                        {
                            check(reopened.WindowState == FormWindowState.Maximized, "Maximized state was not remembered.");
                            reopened.exit_application();
                        }
                        window.restore_window();
                        if (integration)
                        {
                            await browser.active_view.CoreWebView2.ExecuteScriptAsync("window.open('about:blank', 'muse-login-test');");
                            stage = 2;
                        }
                        else { stage = 4; exit_event.Set(); }
                    }
                    else if (stage == 2)
                    {
                        if (browser.view_count < 2) { return; }
                        if (browser.active_view.CoreWebView2 == null) { return; }
                        check_browser_layout(window, browser);
                        check(!Object.ReferenceEquals(original_view, browser.active_view), "Popup was not routed inside the application.");
                        await browser.active_view.CoreWebView2.ExecuteScriptAsync("window.close();");
                        stage = 3;
                    }
                    else if (stage == 3)
                    {
                        if (browser.view_count != 1) { return; }
                        check(Object.ReferenceEquals(original_view, browser.active_view), "Closing the login page did not restore the original page.");
                        check_browser_layout(window, browser);
                        stage = 4;
                        exit_event.Set();
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                    test_timer.Stop();
                    window.exit_application();
                }
                finally { busy = false; }
            };
            test_timer.Start();
            try { Application.Run(window); }
            catch (Exception error) { failure = error; window.Dispose(); }
            test_timer.Stop();
            try
            {
                if (failure != null) { throw new InvalidOperationException("Muse test failed.", failure); }
                check(stage == 4 && window.IsDisposed && !window.tray_visible, "Exit did not remove the tray icon and dispose the application.");
                using (StreamWriter writer = new StreamWriter(output_path, false))
                {
                    writer.WriteLine(integration
                        ? "PASS: direct WebView2 control fills one application window; page/draft survive tray restore; window geometry persists; login popup stays in the same window; exit disposes browser and tray."
                        : "PASS: tray lifecycle, activation, window geometry persistence, and exit cleanup.");
                }
                File.Delete(settings_path);
                return 0;
            }
            catch (Exception error)
            {
                using (StreamWriter writer = new StreamWriter(output_path, false)) { writer.WriteLine(error.ToString()); }
                return 1;
            }
        }
    }
}
