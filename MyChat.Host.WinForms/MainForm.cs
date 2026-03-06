using MyChat.Abstractions;
using MyChat.Factory;

namespace MyChat.Host.WinForms;

public sealed class MainForm : Form
{
    private readonly CheckBox _winFormsCheckBox;
    private readonly CheckBox _wpfCheckBox;
    private readonly CheckBox _webViewCheckBox;
    private readonly Panel _chatHostPanel;
    private readonly Label _statusLabel;
    private readonly TextBox _myMessageTextBox;
    private readonly TextBox _otherMessageTextBox;
    private IMyChatBindable? _chat;

    public MainForm()
    {
        Text = "MyChat Startup Tester";
        Width = 920;
        Height = 640;

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(6, 8, 6, 0)
        };

        _winFormsCheckBox = CreateTechnologyCheckBox("WinForms", ChatUiTechnology.WinForms, true);
        _wpfCheckBox = CreateTechnologyCheckBox("WPF", ChatUiTechnology.Wpf, false);
        _webViewCheckBox = CreateTechnologyCheckBox("WebView2", ChatUiTechnology.WebView2, false);

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "WinForms geladen.",
            Margin = new Padding(12, 8, 0, 0)
        };

        topBar.Controls.Add(new Label { Text = "Technologie:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        topBar.Controls.Add(_winFormsCheckBox);
        topBar.Controls.Add(_wpfCheckBox);
        topBar.Controls.Add(_webViewCheckBox);
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
        addMyMessageButton.Click += (_, _) => AddMessageFromTextBox(_myMessageTextBox, "Matthias");
        messagePanel.Controls.Add(addMyMessageButton, 2, 0);

        messagePanel.Controls.Add(new Label { Text = "From someone else:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, 1);
        _otherMessageTextBox = new TextBox { Dock = DockStyle.Fill };
        messagePanel.Controls.Add(_otherMessageTextBox, 1, 1);
        var addOtherMessageButton = new Button { Text = "Add Message (from someone else)", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        addOtherMessageButton.Click += (_, _) => AddMessageFromTextBox(_otherMessageTextBox, "Support");
        messagePanel.Controls.Add(addOtherMessageButton, 2, 1);

        Controls.Add(_chatHostPanel);
        Controls.Add(messagePanel);
        Controls.Add(topBar);

        LoadChat(ChatUiTechnology.WinForms);
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

    private IEnumerable<CheckBox> GetTechnologyCheckBoxes()
    {
        yield return _winFormsCheckBox;
        yield return _wpfCheckBox;
        yield return _webViewCheckBox;
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
        _chat.BindValues(Program.Memory.Model);

        foreach (var message in Program.Memory.SnapshotMessages())
        {
            _chat.AddMessage(message);
        }

        if (Program.Memory.SnapshotMessages().Count == 0)
        {
            var infoMessage = new ChatMessage
            {
                Sender = "System",
                Text = "Variante ist aktiv.",
                Attachments = ["Einführung.pdf", "Screenshot.png"]
            };
            Program.Memory.AddMessage(infoMessage);
            _chat.AddMessage(infoMessage);
        }

        _chat.ReloadRequested += ChatOnReloadRequested;
        _chat.MessageSubmitted += ChatOnMessageSubmitted;

        _chatHostPanel.Controls.Add(chatControl);
        _statusLabel.Text = $"Geladen: {technology}";
    }

    private void AddMessageFromTextBox(TextBox textBox, string sender)
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

        _chat.AddMessage(message);
        Program.Memory.AddMessage(message);

        textBox.Clear();
        _statusLabel.Text = $"Nachricht von '{sender}' hinzugefügt.";
    }

    private void ChatOnReloadRequested(object? sender, EventArgs e)
    {
        _statusLabel.Text = $"Reload empfangen um {DateTime.Now:HH:mm:ss}";
    }

    private void ChatOnMessageSubmitted(object? sender, ChatMessage message)
    {
        Program.Memory.AddMessage(message);
        _statusLabel.Text = $"Nachricht gesendet um {DateTime.Now:HH:mm:ss}";
    }

}
