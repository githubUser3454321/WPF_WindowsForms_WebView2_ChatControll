using MyChat.Abstractions;
using MyChat.Factory;
using MyChat.Host.WinForms.Sync;

namespace MyChat.Host.WinForms;

public sealed class MainForm : Form
{
    private readonly StartupOptions _options;
    private readonly CheckBox _winFormsCheckBox;
    private readonly CheckBox _wpfCheckBox;
    private readonly CheckBox _webViewCheckBox;
    private readonly CheckBox _apiPollingSyncCheckBox;
    private readonly CheckBox _signalRSyncCheckBox;
    private readonly CheckBox _sseSyncCheckBox;
    private readonly Panel _chatHostPanel;
    private readonly Label _statusLabel;
    private readonly TextBox _myMessageTextBox;
    private readonly TextBox _otherMessageTextBox;
    private readonly List<ChatMessage> _messages = [];
    private readonly HashSet<string> _messageDedup = [];
    private readonly ChatBindModel _model;
    private readonly CancellationTokenSource _syncCts = new();

    private IMyChatBindable? _chat;
    private IChatSyncClient? _syncClient;
    private ChatSyncTechnology _currentSyncTechnology;

    public MainForm(StartupOptions options)
    {
        _options = options;
        _model = new ChatBindModel
        {
            ObjectType = "Invoice",
            RecordId = "123",
            CurrentUser = options.DisplayName
        };

        _currentSyncTechnology = options.InitialSyncTechnology;

        Text = $"MyChat Startup Tester - {_options.DisplayName}";
        Width = 980;
        Height = 680;

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(6, 8, 6, 0)
        };

        _winFormsCheckBox = CreateTechnologyCheckBox("WinForms", ChatUiTechnology.WinForms, false);
        _wpfCheckBox = CreateTechnologyCheckBox("WPF", ChatUiTechnology.Wpf, false);
        _webViewCheckBox = CreateTechnologyCheckBox("WebView2", ChatUiTechnology.WebView2, true);

        _apiPollingSyncCheckBox = CreateSyncCheckBox("Sync: API Polling", ChatSyncTechnology.ApiPolling, _currentSyncTechnology == ChatSyncTechnology.ApiPolling);
        _signalRSyncCheckBox = CreateSyncCheckBox("Sync: SignalR", ChatSyncTechnology.SignalR, _currentSyncTechnology == ChatSyncTechnology.SignalR);
        _sseSyncCheckBox = CreateSyncCheckBox("Sync: SSE", ChatSyncTechnology.ServerSentEvents, _currentSyncTechnology == ChatSyncTechnology.ServerSentEvents);

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = $"Profil {_options.DisplayName} geladen.",
            Margin = new Padding(12, 8, 0, 0)
        };

        topBar.Controls.Add(new Label { Text = "Technologie:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        topBar.Controls.Add(_winFormsCheckBox);
        topBar.Controls.Add(_wpfCheckBox);
        topBar.Controls.Add(_webViewCheckBox);
        topBar.Controls.Add(new Label { Text = "| Realtime:", AutoSize = true, Margin = new Padding(8, 8, 4, 0) });
        topBar.Controls.Add(_apiPollingSyncCheckBox);
        topBar.Controls.Add(_signalRSyncCheckBox);
        topBar.Controls.Add(_sseSyncCheckBox);
        topBar.Controls.Add(_statusLabel);

        _chatHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            BackColor = Color.Gainsboro
        };

        var messagePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 84,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(8)
        };
        messagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        messagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        messagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        messagePanel.Controls.Add(new Label { Text = "From me:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, 0);
        _myMessageTextBox = new TextBox { Dock = DockStyle.Fill };
        messagePanel.Controls.Add(_myMessageTextBox, 1, 0);
        var addMyMessageButton = new Button { Text = "Add Message (from me)", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        addMyMessageButton.Click += async (_, _) => await AddMessageFromTextBoxAsync(_myMessageTextBox, _options.DisplayName, publishRemote: true);
        messagePanel.Controls.Add(addMyMessageButton, 2, 0);

        messagePanel.Controls.Add(new Label { Text = "From someone else:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, 1);
        _otherMessageTextBox = new TextBox { Dock = DockStyle.Fill };
        messagePanel.Controls.Add(_otherMessageTextBox, 1, 1);
        var addOtherMessageButton = new Button { Text = "Add Message (from someone else)", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        addOtherMessageButton.Click += async (_, _) => await AddMessageFromTextBoxAsync(_otherMessageTextBox, "Support", publishRemote: true);
        messagePanel.Controls.Add(addOtherMessageButton, 2, 1);

        Controls.Add(_chatHostPanel);
        Controls.Add(messagePanel);
        Controls.Add(topBar);

        LoadChat(ChatUiTechnology.WebView2);
        _ = StartSyncAsync(_currentSyncTechnology);
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        _syncCts.Cancel();
        if (_syncClient is not null)
        {
            await _syncClient.DisposeAsync();
        }

        base.OnFormClosed(e);
    }

    private CheckBox CreateTechnologyCheckBox(string label, ChatUiTechnology technology, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Text = label,
            AutoSize = true,
            Checked = isChecked,
            Tag = technology,
            Margin = new Padding(0, 6, 10, 0)
        };

        checkBox.CheckedChanged += TechnologyCheckBoxOnCheckedChanged;
        return checkBox;
    }

    private CheckBox CreateSyncCheckBox(string label, ChatSyncTechnology technology, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Text = label,
            AutoSize = true,
            Checked = isChecked,
            Tag = technology,
            Margin = new Padding(0, 6, 10, 0)
        };

        checkBox.CheckedChanged += SyncCheckBoxOnCheckedChanged;
        return checkBox;
    }

    private void TechnologyCheckBoxOnCheckedChanged(object? sender, EventArgs e)
    {
        if (sender is not CheckBox changedCheckBox || !changedCheckBox.Checked)
        {
            return;
        }

        foreach (var checkBox in GetTechnologyCheckBoxes())
        {
            if (!ReferenceEquals(checkBox, changedCheckBox))
            {
                checkBox.CheckedChanged -= TechnologyCheckBoxOnCheckedChanged;
                checkBox.Checked = false;
                checkBox.CheckedChanged += TechnologyCheckBoxOnCheckedChanged;
            }
        }

        if (changedCheckBox.Tag is ChatUiTechnology technology)
        {
            LoadChat(technology);
        }
    }

    private async void SyncCheckBoxOnCheckedChanged(object? sender, EventArgs e)
    {
        if (sender is not CheckBox changedCheckBox || !changedCheckBox.Checked)
        {
            return;
        }

        foreach (var checkBox in GetSyncCheckBoxes())
        {
            if (!ReferenceEquals(checkBox, changedCheckBox))
            {
                checkBox.CheckedChanged -= SyncCheckBoxOnCheckedChanged;
                checkBox.Checked = false;
                checkBox.CheckedChanged += SyncCheckBoxOnCheckedChanged;
            }
        }

        if (changedCheckBox.Tag is ChatSyncTechnology technology)
        {
            _currentSyncTechnology = technology;
            await StartSyncAsync(technology);
        }
    }

    private IEnumerable<CheckBox> GetTechnologyCheckBoxes()
    {
        yield return _winFormsCheckBox;
        yield return _wpfCheckBox;
        yield return _webViewCheckBox;
    }

    private IEnumerable<CheckBox> GetSyncCheckBoxes()
    {
        yield return _apiPollingSyncCheckBox;
        yield return _signalRSyncCheckBox;
        yield return _sseSyncCheckBox;
    }

    private async Task StartSyncAsync(ChatSyncTechnology technology)
    {
        if (_syncClient is not null)
        {
            await _syncClient.DisposeAsync();
        }

        _syncClient = ChatSyncClientFactory.Create(technology, _options.SyncServiceUri, _options.SyncChannel);
        _syncClient.MessageReceived += SyncClientOnMessageReceived;
        await _syncClient.StartAsync(_syncCts.Token);

        _statusLabel.Text = $"Sync aktiv: {technology} ({_options.DisplayName})";
    }

    private void SyncClientOnMessageReceived(object? sender, ChatSyncMessageDto message)
    {
        if (message.Sender.Equals(_options.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        BeginInvoke(() =>
        {
            AddMessageLocal(new ChatMessage
            {
                Sender = message.Sender,
                Text = message.Text,
                TimestampUtc = message.SentAtUtc
            });

            _statusLabel.Text = $"Remote Nachricht von {message.Sender} empfangen ({_currentSyncTechnology}).";
        });
    }

    private void LoadChat(ChatUiTechnology technology)
    {
        if (_chat is not null)
        {
            _chat.ReloadRequested -= ChatOnReloadRequested;
            _chat.MessageSubmitted -= ChatOnMessageSubmitted;
        }

        _chatHostPanel.Controls.Clear();
        _chat = MyChatControlFactory.Create(technology);
        var chatControl = MyChatControlFactory.AsControl(_chat);

        chatControl.Dock = DockStyle.Fill;
        chatControl.Margin = new Padding(0);

        _chat.HeaderHeight = 32;
        _chat.RowHeight = 24;
        _chat.BindValues(_model);

        foreach (var message in _messages)
        {
            _chat.AddMessage(message);
        }

        if (_messages.Count == 0)
        {
            AddMessageLocal(new ChatMessage
            {
                Sender = "System",
                Text = "Variante ist aktiv.",
                Attachments = ["Einführung.pdf", "Screenshot.png"]
            });
        }

        _chat.ReloadRequested += ChatOnReloadRequested;
        _chat.MessageSubmitted += ChatOnMessageSubmitted;

        _chatHostPanel.Controls.Add(chatControl);
        _statusLabel.Text = $"Geladen: {technology} für {_options.DisplayName}";
    }

    private async Task AddMessageFromTextBoxAsync(TextBox textBox, string sender, bool publishRemote)
    {
        if (_chat is null)
        {
            _statusLabel.Text = "Bitte zuerst eine Technologie wählen.";
            return;
        }

        var text = textBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _statusLabel.Text = "Bitte zuerst Text eingeben.";
            return;
        }

        var message = new ChatMessage
        {
            Sender = sender,
            Text = text
        };

        AddMessageLocal(message);

        if (publishRemote && _syncClient is not null)
        {
            await _syncClient.PublishAsync(new ChatSyncMessageDto
            {
                Sender = sender,
                Text = text,
                Channel = _options.SyncChannel,
                SentAtUtc = message.TimestampUtc
            }, _syncCts.Token);
        }

        textBox.Clear();
        _statusLabel.Text = $"Nachricht von '{sender}' hinzugefügt.";
    }

    private void AddMessageLocal(ChatMessage message)
    {
        var signature = $"{message.Sender}|{message.Text}|{message.TimestampUtc:O}";
        if (!_messageDedup.Add(signature))
        {
            return;
        }

        _messages.Add(message);
        _chat?.AddMessage(message);
    }

    private void ChatOnReloadRequested(object? sender, EventArgs e)
    {
        _statusLabel.Text = $"Reload empfangen um {DateTime.Now:HH:mm:ss}";
    }

    private async void ChatOnMessageSubmitted(object? sender, ChatMessage message)
    {
        message.Sender = _options.DisplayName;
        AddMessageLocal(message);

        if (_syncClient is not null)
        {
            await _syncClient.PublishAsync(new ChatSyncMessageDto
            {
                Sender = message.Sender,
                Text = message.Text,
                Channel = _options.SyncChannel,
                SentAtUtc = message.TimestampUtc
            }, _syncCts.Token);
        }

        _statusLabel.Text = $"Nachricht gesendet um {DateTime.Now:HH:mm:ss}";
    }
}
