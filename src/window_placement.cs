using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal sealed class WindowPlacementData
{
    public int left { get; set; }
    public int top { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public bool maximized { get; set; }
}

internal sealed class WindowPlacement
{
    private readonly string _settings_path;
    private Rectangle _normal_bounds;
    private bool _maximized;

    internal WindowPlacement(string settings_path)
    {
        _settings_path = settings_path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "Local", "MuseDesktop", "window_state.json");
    }

    internal void restore(Form window)
    {
        _normal_bounds = window.Bounds;
        if (!File.Exists(_settings_path)) { return; }
        try
        {
            string settings_json;
            using (StreamReader reader = new StreamReader(_settings_path, Encoding.UTF8))
            {
                settings_json = reader.ReadToEnd();
            }
            WindowPlacementData settings = new JavaScriptSerializer().Deserialize<WindowPlacementData>(settings_json);
            if (settings == null || settings.width < window.MinimumSize.Width
                || settings.height < window.MinimumSize.Height || settings.width > 32768 || settings.height > 32768
                || settings.left < -32768 || settings.left > 32768 || settings.top < -32768 || settings.top > 32768)
            {
                return;
            }
            Rectangle bounds = new Rectangle(settings.left, settings.top, settings.width, settings.height);
            Rectangle working_area = Screen.FromRectangle(bounds).WorkingArea;
            bounds.Width = Math.Max(window.MinimumSize.Width, Math.Min(bounds.Width, working_area.Width));
            bounds.Height = Math.Max(window.MinimumSize.Height, Math.Min(bounds.Height, working_area.Height));
            bounds.X = Math.Max(working_area.Left, Math.Min(bounds.X, working_area.Right - bounds.Width));
            bounds.Y = Math.Max(working_area.Top, Math.Min(bounds.Y, working_area.Bottom - bounds.Height));
            window.StartPosition = FormStartPosition.Manual;
            window.Bounds = bounds;
            _normal_bounds = bounds;
            _maximized = settings.maximized;
            if (_maximized) { window.WindowState = FormWindowState.Maximized; }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
    }

    internal void track(Form window)
    {
        if (window.WindowState == FormWindowState.Minimized) { return; }
        _maximized = window.WindowState == FormWindowState.Maximized;
        if (!_maximized) { _normal_bounds = window.Bounds; }
    }

    internal void save(Form window)
    {
        track(window);
        WindowPlacementData settings = new WindowPlacementData();
        settings.left = _normal_bounds.Left;
        settings.top = _normal_bounds.Top;
        settings.width = _normal_bounds.Width;
        settings.height = _normal_bounds.Height;
        settings.maximized = _maximized;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settings_path));
            using (StreamWriter writer = new StreamWriter(_settings_path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(new JavaScriptSerializer().Serialize(settings));
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
