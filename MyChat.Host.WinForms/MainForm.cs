using MyChat.Abstractions;
using MyChat.Factory;

namespace MyChat.Host.WinForms;

public sealed class MainForm : Form
{
    private readonly ComboBox _technologySelector;
    private readonly Button _loadButton;
    private readonly Panel _chatHostPanel;
    private readonly Label _statusLabel;
    private IMyChatBindable? _chat;

    public MainForm()
    {
        Text = "MyChat Host (WinForms)";
        Width = 820;
        Height = 560;

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(6, 4, 6, 0)
        };

        _technologySelector = new ComboBox
        {
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = Enum.GetValues(typeof(ChatUiTechnology))
        };

        _loadButton = new Button { Text = "Variante laden", Width = 120 };
        _loadButton.Click += (_, _) => LoadSelectedChat();

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "Bitte Variante wählen und laden.",
            Margin = new Padding(12, 7, 0, 0)
        };

        topBar.Controls.Add(new Label { Text = "Technologie:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        topBar.Controls.Add(_technologySelector);
        topBar.Controls.Add(_loadButton);
        topBar.Controls.Add(_statusLabel);

        _chatHostPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BackColor = Color.Gainsboro };

        Controls.Add(_chatHostPanel);
        Controls.Add(topBar);
    }

    private void LoadSelectedChat()
    {
        if (_technologySelector.SelectedItem is not ChatUiTechnology technology)
        {
            return;
        }

        _chatHostPanel.Controls.Clear();
        _chat = MyChatControlFactory.Create(technology);
        var chatControl = MyChatControlFactory.AsControl(_chat);

        chatControl.Location = new Point(0, 0);
        chatControl.Margin = new Padding(0);
        chatControl.Size = new Size(760, 460);
        chatControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        _chat.HeaderHeight = 32;
        _chat.RowHeight = 24;
        _chat.BindValues(new ChatBindModel
        {
            ObjectType = "Invoice",
            RecordId = "123",
            CurrentUser = "Matthias"
        });
        _chat.AddMessage(new ChatMessage { Sender = "System", Text = $"Variante {technology} ist aktiv." });
        _chat.ReloadRequested += ChatOnReloadRequested;

        _chatHostPanel.Controls.Add(chatControl);
        _statusLabel.Text = $"Geladen: {technology}";
    }

    private void ChatOnReloadRequested(object? sender, EventArgs e)
    {
        _statusLabel.Text = $"Reload empfangen um {DateTime.Now:HH:mm:ss}";
    }
}
