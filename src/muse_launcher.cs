using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

internal static class MuseLauncher
{
    private const string APP_URL = "https://muse.ai/";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        try
        {
            string data_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "Local", "MuseDesktop", "WebView2");
            if (args.Length == 1 && args[0] == "--diagnose")
            {
                Dictionary<string, object> diagnostics = new Dictionary<string, object>();
                diagnostics["app_url"] = APP_URL;
                diagnostics["mode"] = "native_webview2";
                diagnostics["user_data_directory"] = data_path;
                diagnostics["runtime_version"] = CoreWebView2Environment.GetAvailableBrowserVersionString();
                diagnostics["close_behavior"] = "hide_to_tray";
                diagnostics["single_instance"] = true;
                using (StreamWriter writer = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.json"), false, new UTF8Encoding(false)))
                {
                    writer.WriteLine(new JavaScriptSerializer().Serialize(diagnostics));
                }
                return 0;
            }
            if (args.Length != 0) { throw new ArgumentException("不支持的启动参数。"); }
            using (EventWaitHandle activate_event = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Muse.Desktop.Activate"))
            using (EventWaitHandle exit_event = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Muse.Desktop.Exit"))
            {
                bool is_primary;
                using (Mutex instance_mutex = new Mutex(true, "Local\\Muse.Desktop.Instance", out is_primary))
                {
                    if (!is_primary) { activate_event.Set(); return 0; }
                    try
                    {
                        using (MuseWindow window = new MuseWindow(delegate(Control host)
                        {
                            return new WebAppView(host, data_path, APP_URL);
                        }, activate_event, exit_event))
                        {
                            Application.Run(window);
                        }
                    }
                    finally { instance_mutex.ReleaseMutex(); }
                }
            }
            return 0;
        }
        catch (WebView2RuntimeNotFoundException) { return show_error("请先安装 Microsoft Edge WebView2 Runtime。"); }
        catch (UnauthorizedAccessException error) { return show_error("无法读取应用配置：" + error.Message); }
        catch (IOException error) { return show_error("无法读取应用文件：" + error.Message); }
        catch (COMException error) { return show_error("网页组件启动失败：" + error.Message); }
        catch (InvalidOperationException error) { return show_error(error.Message); }
        catch (ArgumentException error) { return show_error(error.Message); }
    }

    private static int show_error(string message)
    {
        MessageBox.Show(message, "Muse", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return 1;
    }
}
