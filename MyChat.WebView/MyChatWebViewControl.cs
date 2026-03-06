using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MyChat.Abstractions;

namespace MyChat.WebView;

public sealed class MyChatWebViewControl : UserControl, IMyChatBindable
{
    private readonly WebView2 _webView;
    private readonly ScriptBridge _bridge;
    private ChatBindModel? _pendingModel;
    private int _headerHeight = 32;
    private int _rowHeight = 24;
    private bool _isReady;

    public MyChatWebViewControl()
    {
        _bridge = new ScriptBridge();
        _bridge.ReloadRequested += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);
        _ = InitializeAsync();
    }

    [Browsable(true)]
    [DefaultValue(32)]
    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            _headerHeight = Math.Max(16, value);
            _ = PushSettingsAsync();
        }
    }

    [Browsable(true)]
    [DefaultValue(24)]
    public int RowHeight
    {
        get => _rowHeight;
        set
        {
            _rowHeight = Math.Max(16, value);
            _ = PushSettingsAsync();
        }
    }

    public ChatBindModel? BoundModel { get; private set; }

    public event EventHandler? ReloadRequested;

    public void BindValues(ChatBindModel model)
    {
        BoundModel = model;
        _pendingModel = model;
        _ = PushSettingsAsync();
    }

    public void AddMessage(ChatMessage message)
    {
        if (!_isReady)
        {
            return;
        }

        var js = $$"window.chatInterop.addMessage({{ sender: {{ToJs(message.Sender)}}, text: {{ToJs(message.Text)}}, time: {{ToJs(message.TimestampUtc.ToString("HH:mm"))}} }});";
        _ = _webView.CoreWebView2.ExecuteScriptAsync(js);
    }

    private async Task InitializeAsync()
    {
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
        _webView.CoreWebView2.AddHostObjectToScript("chatBridge", _bridge);

        var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        _webView.CoreWebView2.Navigate(new Uri(indexPath).AbsoluteUri);
        _webView.CoreWebView2.NavigationCompleted += CoreWebView2OnNavigationCompleted;
    }

    private async void CoreWebView2OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _isReady = e.IsSuccess;
        if (!_isReady)
        {
            return;
        }

        await PushSettingsAsync();
    }

    private async Task PushSettingsAsync()
    {
        if (!_isReady)
        {
            return;
        }

        var model = _pendingModel ?? BoundModel;
        var payload = model is null
            ? "{}"
            : $$"{\"objectType\":{{ToJs(model.ObjectType)}},\"recordId\":{{ToJs(model.RecordId)}},\"currentUser\":{{ToJs(model.CurrentUser)}}}";

        var js = $$"window.chatInterop.applySettings({ headerHeight: {{_headerHeight}}, rowHeight: {{_rowHeight}}, model: {{payload}} });";
        await _webView.CoreWebView2.ExecuteScriptAsync(js);
    }

    private static string ToJs(string value)
    {
        return System.Text.Json.JsonSerializer.Serialize(value);
    }

    [ComVisible(true)]
    public sealed class ScriptBridge
    {
        public event EventHandler? ReloadRequested;

        public void NotifyReloadRequested()
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
