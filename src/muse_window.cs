using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

internal sealed class MuseWindow : Form
{
    private readonly IAppView _app_view;
    private readonly NotifyIcon _tray_icon;
    private readonly ContextMenuStrip _tray_menu;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly EventWaitHandle _activate_event;
    private readonly EventWaitHandle _exit_event;
    private readonly Label _status_label;
    private readonly Button _retry_button;
    private readonly WindowPlacement _placement;
    private bool _exit_requested;
    private bool _exit_pending;
    private bool _was_ready;

    internal MuseWindow(Func<Control, IAppView> create_app_view, EventWaitHandle activate_event, EventWaitHandle exit_event, string settings_path = null)
    {
        _activate_event = activate_event;
        _exit_event = exit_event;
        Text = "Muse";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1200, 800);
        MinimumSize = new Size(640, 480);
        _placement = new WindowPlacement(settings_path);
        _placement.restore(this);
        BackColor = Color.FromArgb(245, 242, 250);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _status_label = new Label();
        _status_label.Text = "正在打开 Muse…";
        _status_label.Dock = DockStyle.Fill;
        _status_label.TextAlign = ContentAlignment.MiddleCenter;
        _status_label.Font = new Font("Microsoft YaHei UI", 11);
        _retry_button = new Button();
        _retry_button.Text = "重新打开 Muse";
        _retry_button.Dock = DockStyle.Bottom;
        _retry_button.Height = 48;
        _retry_button.Visible = false;
        _retry_button.Click += delegate { start_page(); };
        Controls.Add(_status_label);
        Controls.Add(_retry_button);
        _app_view = create_app_view(this);

        _tray_menu = new ContextMenuStrip();
        _tray_menu.Items.Add("打开 Muse", null, delegate { restore_window(); });
        _tray_menu.Items.Add("隐藏到托盘", null, delegate { hide_to_tray(); });
        _tray_menu.Items.Add(new ToolStripSeparator());
        _tray_menu.Items.Add("退出 Muse", null, delegate { exit_application(); });
        _tray_icon = new NotifyIcon();
        _tray_icon.Icon = Icon;
        _tray_icon.Text = "Muse · 双击打开，右键退出";
        _tray_icon.ContextMenuStrip = _tray_menu;
        _tray_icon.DoubleClick += delegate { restore_window(); };
        _tray_icon.Visible = true;

        _timer = new System.Windows.Forms.Timer();
        _timer.Interval = 250;
        _timer.Tick += delegate { poll_state(); };
        Shown += delegate { _placement.track(this); start_page(); _timer.Start(); };
        LocationChanged += delegate { _placement.track(this); sync_page(false); };
        Resize += delegate { _placement.track(this); sync_page(false); };
        ResizeEnd += delegate { _placement.save(this); };
        Activated += delegate { sync_page(true); };
    }

    internal bool tray_visible { get { return _tray_icon.Visible; } }
    internal IAppView app_view { get { return _app_view; } }

    private void start_page()
    {
        _status_label.Text = "正在打开 Muse…";
        _retry_button.Visible = false;
        _app_view.start();
    }

    private void poll_state()
    {
        if (_exit_event.WaitOne(0))
        {
            exit_application();
            return;
        }
        if (_activate_event.WaitOne(0))
        {
            restore_window();
        }
        _app_view.poll();
        if (_app_view.is_ready)
        {
            if (!_was_ready)
            {
                _was_ready = true;
                _retry_button.Visible = false;
                sync_page(true);
            }
        }
        else if (!_app_view.is_starting)
        {
            if (_was_ready)
            {
                _was_ready = false;
                hide_to_tray();
            }
            _status_label.Text = String.IsNullOrEmpty(_app_view.error_message)
                ? "点击下方按钮重新打开 Muse。" : _app_view.error_message;
            _retry_button.Visible = true;
        }
    }

    private void sync_page(bool activate)
    {
        if (_app_view == null)
        {
            return;
        }
        if (!Visible || WindowState == FormWindowState.Minimized)
        {
            _app_view.hide();
            return;
        }
        _app_view.show(new Rectangle(Point.Empty, ClientSize), activate);
    }

    internal void hide_to_tray()
    {
        _placement.save(this);
        _app_view.hide();
        Hide();
    }

    internal void restore_window()
    {
        Show();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
        if (!_app_view.is_ready && !_app_view.is_starting)
        {
            start_page();
        }
        sync_page(true);
    }

    internal async void exit_application()
    {
        if (_exit_pending || _exit_requested || IsDisposed) { return; }
        _exit_pending = true;
        await _app_view.save_state_async();
        if (IsDisposed) { return; }
        _exit_requested = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs event_args)
    {
        if (!_exit_requested && event_args.CloseReason == CloseReason.UserClosing)
        {
            event_args.Cancel = true;
            hide_to_tray();
        }
        else
        {
            _placement.save(this);
            _timer.Stop();
            _tray_icon.Visible = false;
            _app_view.close();
        }
        base.OnFormClosing(event_args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_timer != null) { _timer.Dispose(); }
            if (_tray_icon != null) { _tray_icon.Visible = false; _tray_icon.Dispose(); }
            if (_tray_menu != null) { _tray_menu.Dispose(); }
            if (_app_view != null) { _app_view.Dispose(); }
            if (_status_label != null) { _status_label.Font.Dispose(); }
            if (Icon != null) { Icon.Dispose(); }
        }
        base.Dispose(disposing);
    }
}
