using System.ComponentModel;
using MyChat.Abstractions;

namespace MyChat.WinForms;

public sealed class MyChatWinFormsControl : UserControl, IMyChatBindable
{
    private readonly Panel _headerPanel;
    private readonly Label _headerLabel;
    private readonly ListBox _messagesList;
    private readonly TextBox _inputBox;
    private readonly Button _sendButton;
    private readonly Button _reloadButton;
    private readonly TableLayoutPanel _layout;

    private int _headerHeight = 32;
    private int _rowHeight = 24;

    public MyChatWinFormsControl()
    {
        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, _headerHeight));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(36, 65, 121) };
        _headerLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Chat (WinForms)",
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        _headerPanel.Controls.Add(_headerLabel);

        _messagesList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = _rowHeight
        };
        _messagesList.DrawItem += DrawMessageItem;

        var inputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        _inputBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Nachricht ..." };
        _sendButton = new Button { Dock = DockStyle.Fill, Text = "Senden" };
        _reloadButton = new Button { Dock = DockStyle.Fill, Text = "Reload" };
        _sendButton.Click += (_, _) => SendCurrentText();
        _reloadButton.Click += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);

        inputPanel.Controls.Add(_inputBox, 0, 0);
        inputPanel.Controls.Add(_sendButton, 1, 0);
        inputPanel.Controls.Add(_reloadButton, 2, 0);

        _layout.Controls.Add(_headerPanel, 0, 0);
        _layout.Controls.Add(_messagesList, 0, 1);
        _layout.Controls.Add(inputPanel, 0, 2);

        Controls.Add(_layout);
    }

    [Browsable(true)]
    [DefaultValue(32)]
    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            _headerHeight = Math.Max(16, value);
            _layout.RowStyles[0].Height = _headerHeight;
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
            _messagesList.ItemHeight = _rowHeight;
            _messagesList.Invalidate();
        }
    }

    public ChatBindModel? BoundModel { get; private set; }

    public event EventHandler? ReloadRequested;

    public void BindValues(ChatBindModel model)
    {
        BoundModel = model;
        _headerLabel.Text = $"Chat (WinForms) | {model.ObjectType}/{model.RecordId} | User: {model.CurrentUser}";
    }

    public void AddMessage(ChatMessage message)
    {
        _messagesList.Items.Add($"[{message.TimestampUtc:HH:mm}] {message.Sender}: {message.Text}");
        _messagesList.TopIndex = _messagesList.Items.Count - 1;
    }

    private void SendCurrentText()
    {
        if (string.IsNullOrWhiteSpace(_inputBox.Text))
        {
            return;
        }

        var user = BoundModel?.CurrentUser ?? "Host";
        AddMessage(new ChatMessage { Sender = user, Text = _inputBox.Text.Trim() });
        _inputBox.Clear();
    }

    private void DrawMessageItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        e.DrawBackground();
        var item = _messagesList.Items[e.Index]?.ToString() ?? string.Empty;
        TextRenderer.DrawText(e.Graphics, item, e.Font, e.Bounds, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }
}
