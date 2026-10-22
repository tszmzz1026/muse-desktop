using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal interface IAppView : IDisposable
{
    bool is_ready { get; }
    bool is_starting { get; }
    string error_message { get; }
    void start();
    void poll();
    void show(Rectangle bounds, bool activate);
    void hide();
    void close();
}

internal sealed class WebAppView : IAppView
{
    private readonly Control _host;
    private readonly string _data_path;
    private readonly string _start_url;
    private readonly List<WebView2> _views = new List<WebView2>();
    private CoreWebView2Environment _environment;
    private bool _disposed;

    internal WebAppView(Control host, string data_path, string start_url)
    {
        _host = host;
        _data_path = data_path;
        _start_url = start_url;
        error_message = "";
    }

    public bool is_ready { get; private set; }
    public bool is_starting { get; private set; }
    public string error_message { get; private set; }
    internal WebView2 active_view { get { return _views.Count == 0 ? null : _views[_views.Count - 1]; } }
    internal int view_count { get { return _views.Count; } }

    private WebView2 create_view()
    {
        WebView2 view = new WebView2();
        view.Dock = DockStyle.Fill;
        view.DefaultBackgroundColor = Color.White;
        view.Visible = false;
        _host.Controls.Add(view);
        _views.Add(view);
        return view;
    }

    private void configure_view(WebView2 view)
    {
        view.CoreWebView2.NewWindowRequested += open_popup;
        view.CoreWebView2.WindowCloseRequested += delegate
        {
            if (_views.Count > 1)
            {
                _views.Remove(view);
                view.Dispose();
                show(Rectangle.Empty, true);
            }
            else { _host.FindForm().Close(); }
        };
    }

    public async void start()
    {
        if (is_ready || is_starting || _disposed) { return; }
        is_starting = true;
        error_message = "";
        try
        {
            Directory.CreateDirectory(_data_path);
            _environment = await CoreWebView2Environment.CreateAsync(null, _data_path);
            if (_disposed) { return; }
            WebView2 view = create_view();
            await view.EnsureCoreWebView2Async(_environment);
            if (_disposed) { return; }
            configure_view(view);
            view.CoreWebView2.Navigate(_start_url);
            is_ready = true;
            show(Rectangle.Empty, false);
        }
        catch (WebView2RuntimeNotFoundException) { error_message = "请先安装 Microsoft Edge WebView2 Runtime。"; }
        catch (COMException error) { error_message = "网页组件启动失败：" + error.Message; }
        catch (InvalidOperationException error) { if (!_disposed) { error_message = "网页组件初始化失败：" + error.Message; } }
        catch (UnauthorizedAccessException error) { error_message = "无法保存网站登录状态：" + error.Message; }
        catch (IOException error) { error_message = "无法打开网站数据目录：" + error.Message; }
        catch (ArgumentException error) { error_message = "网页组件配置无效：" + error.Message; }
        finally
        {
            is_starting = false;
            if (!is_ready && !_disposed)
            {
                foreach (WebView2 view in _views) { view.Dispose(); }
                _views.Clear();
            }
        }
    }

    private async void open_popup(object sender, CoreWebView2NewWindowRequestedEventArgs event_args)
    {
        event_args.Handled = true;
        CoreWebView2Deferral deferral = event_args.GetDeferral();
        WebView2 previous_view = active_view;
        WebView2 popup_view = null;
        try
        {
            if (_disposed) { return; }
            popup_view = create_view();
            await popup_view.EnsureCoreWebView2Async(_environment);
            if (_disposed) { return; }
            configure_view(popup_view);
            event_args.NewWindow = popup_view.CoreWebView2;
            previous_view.Visible = false;
            show(Rectangle.Empty, true);
        }
        catch (COMException) { remove_failed_popup(popup_view); }
        catch (InvalidOperationException) { remove_failed_popup(popup_view); }
        catch (ArgumentException) { remove_failed_popup(popup_view); }
        finally { deferral.Complete(); }
    }

    private void remove_failed_popup(WebView2 popup_view)
    {
        if (popup_view != null)
        {
            _views.Remove(popup_view);
            popup_view.Dispose();
        }
        if (!_disposed) { show(Rectangle.Empty, false); }
    }

    public void poll() { }

    public void show(Rectangle bounds, bool activate)
    {
        WebView2 view = active_view;
        if (_disposed || view == null || !is_ready) { return; }
        view.Visible = true;
        view.BringToFront();
        if (activate && _host.Visible) { view.Focus(); }
    }

    public void hide()
    {
        foreach (WebView2 view in _views) { view.Visible = false; }
    }

    public void close()
    {
        if (_disposed) { return; }
        _disposed = true;
        is_ready = false;
        is_starting = false;
        foreach (WebView2 view in _views) { view.Dispose(); }
        _views.Clear();
    }

    public void Dispose() { close(); }
}
