using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MyChat.Abstractions;

namespace MyChat.WebView;

public sealed class MyChatWebViewControl : UserControl, IMyChatBindable
{
    private static Func<Control> s_webViewFactory = static () => new WebView2();

    private readonly WebView2 _webView;
    private readonly ScriptBridge _bridge;
    private ChatBindModel? _pendingModel;
    private int _headerHeight = 32;
    private int _rowHeight = 24;
    private bool _isReady;
    private readonly List<ChatMessage> _pendingMessages = [];

    public MyChatWebViewControl()
    {
        _bridge = new ScriptBridge();
        _bridge.ReloadRequested += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);
        _bridge.MessageSubmitted += (_, message) => MessageSubmitted?.Invoke(this, message);

        _webView = CreateWebView2FromFactory();
        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);
        _ = InitializeAsync();
    }

    public static void UseWebViewFactory(Func<Control> webViewFactory)
    {
        ArgumentNullException.ThrowIfNull(webViewFactory);
        s_webViewFactory = webViewFactory;
    }

    public static void UseWebViewFactory<TWebView2>() where TWebView2 : WebView2, new()
    {
        s_webViewFactory = static () => new TWebView2();
    }

    public static void ResetWebViewFactory()
    {
        s_webViewFactory = static () => new WebView2();
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

    public event EventHandler<ChatMessage>? MessageSubmitted;

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
            _pendingMessages.Add(message);
            return;
        }

        _ = AddMessageAsync(message);
    }

    private static WebView2 CreateWebView2FromFactory()
    {
        var created = s_webViewFactory();
        return created as WebView2
            ?? throw new InvalidOperationException(
                $"Configured WebView factory must return a '{typeof(WebView2).FullName}' control.");
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

        foreach (var pendingMessage in _pendingMessages)
        {
            await AddMessageAsync(pendingMessage);
        }

        _pendingMessages.Clear();
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
            : JsonSerializer.Serialize(new
            {
                objectType = model.ObjectType,
                recordId = model.RecordId,
                currentUser = model.CurrentUser
            });

        var js = $"window.chatInterop.applySettings({{ headerHeight: {_headerHeight}, rowHeight: {_rowHeight}, model: {payload} }});";
        await _webView.CoreWebView2.ExecuteScriptAsync(js);
    }

    private Task AddMessageAsync(ChatMessage message)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sender = message.Sender,
            text = message.Text,
            time = message.TimestampUtc.ToLocalTime().ToString("dd.MM.yyyy, HH:mm:ss"),
            attachments = message.Attachments
        });

        var js = $"window.chatInterop.addMessage({payload});";
        return _webView.CoreWebView2.ExecuteScriptAsync(js);
    }

    [ComVisible(true)]
    public sealed class ScriptBridge
    {
        public event EventHandler? ReloadRequested;

        public event EventHandler<ChatMessage>? MessageSubmitted;

        public void NotifyReloadRequested()
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SubmitMessage(string sender, string text, string[] attachments)
        {
            MessageSubmitted?.Invoke(this, new ChatMessage
            {
                Sender = sender,
                Text = text,
                Attachments = attachments?.ToList() ?? []
            });
        }
    }
}
