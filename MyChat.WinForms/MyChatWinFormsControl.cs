using System.ComponentModel;
using MyChat.Abstractions;

namespace MyChat.WinForms;

public sealed class MyChatWinFormsControl : UserControl, IMyChatBindable
{
    private readonly Panel _headerPanel;
    private readonly Label _headerLabel;
    private readonly FlowLayoutPanel _messageFlow;
    private readonly Panel _messageHost;
    private readonly TextBox _inputBox;
    private readonly Button _sendButton;
    private readonly Button _reloadButton;
    private readonly TableLayoutPanel _layout;

    private int _headerHeight = 32;
    private int _rowHeight = 24;

    public MyChatWinFormsControl()
    {
        BackColor = Color.FromArgb(245, 247, 251);

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, _headerHeight));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        _headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(36, 65, 121) };
        _headerLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Chat (WinForms)",
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0)
        };
        _headerPanel.Controls.Add(_headerLabel);

        _messageHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 12),
            BackColor = BackColor,
            AutoScroll = true
        };

        _messageFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = BackColor
        };

        _messageHost.Controls.Add(_messageFlow);
        _messageHost.Resize += (_, _) => ReflowMessages();

        var inputPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 8)
        };
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        _inputBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Nachricht ...", BorderStyle = BorderStyle.FixedSingle };
        _sendButton = new Button { Dock = DockStyle.Fill, Text = "➤", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 65, 121), ForeColor = Color.White };
        _reloadButton = new Button { Dock = DockStyle.Fill, Text = "Reload" };

        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Click += (_, _) => SendCurrentText();
        _reloadButton.Click += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);

        inputPanel.Controls.Add(_inputBox, 0, 0);
        inputPanel.Controls.Add(_sendButton, 1, 0);
        inputPanel.Controls.Add(_reloadButton, 2, 0);

        _layout.Controls.Add(_headerPanel, 0, 0);
        _layout.Controls.Add(_messageHost, 0, 1);
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
            ReflowMessages();
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
        var isOwnMessage = IsOwnMessage(message.Sender);
        var wrapper = CreateMessageItem(message, isOwnMessage);
        _messageFlow.Controls.Add(wrapper);
        ReflowMessages();
        _messageHost.ScrollControlIntoView(wrapper);
    }

    private void SendCurrentText()
    {
        if (string.IsNullOrWhiteSpace(_inputBox.Text))
        {
            return;
        }

        var user = BoundModel?.CurrentUser ?? "Ich";
        AddMessage(new ChatMessage { Sender = user, Text = _inputBox.Text.Trim() });
        _inputBox.Clear();
    }

    private bool IsOwnMessage(string sender)
    {
        if (string.Equals(sender, "Ich", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(BoundModel?.CurrentUser)
            && string.Equals(sender, BoundModel.CurrentUser, StringComparison.OrdinalIgnoreCase);
    }

    private Control CreateMessageItem(ChatMessage message, bool isOwnMessage)
    {
        var wrapper = new Panel
        {
            Width = _messageHost.ClientSize.Width - 32,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackColor,
            Margin = new Padding(0, 0, 0, 12),
            Tag = isOwnMessage
        };

        var card = new Panel
        {
            Width = Math.Max(220, (int)(wrapper.Width * 0.72)),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            BackColor = isOwnMessage ? Color.FromArgb(220, 238, 255) : Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = isOwnMessage ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Top | AnchorStyles.Left,
            Location = new Point(isOwnMessage ? wrapper.Width - Math.Max(220, (int)(wrapper.Width * 0.72)) : 0, 0)
        };

        var meta = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(62, 72, 85),
            Text = $"{(isOwnMessage ? "Ich" : message.Sender)} · {message.TimestampUtc.ToLocalTime():dd.MM.yyyy, HH:mm:ss}",
            MaximumSize = new Size(card.Width - 24, 0)
        };

        var text = new Label
        {
            AutoSize = true,
            Text = message.Text,
            MaximumSize = new Size(card.Width - 24, 0),
            Margin = new Padding(0, 8, 0, 0)
        };

        card.Controls.Add(meta);
        card.Controls.Add(text);

        var currentY = meta.Bottom + 8;
        text.Location = new Point(0, currentY);
        currentY = text.Bottom + 8;

        if (message.Attachments.Count > 0)
        {
            var separator = new Label
            {
                Text = "Anhänge",
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 98, 112),
                Font = new Font(Font, FontStyle.Italic),
                Location = new Point(0, currentY)
            };
            card.Controls.Add(separator);
            currentY = separator.Bottom + 4;

            foreach (var attachment in message.Attachments)
            {
                var link = new LinkLabel
                {
                    AutoSize = true,
                    Text = $"📎 {attachment}",
                    Location = new Point(0, currentY)
                };
                card.Controls.Add(link);
                currentY = link.Bottom + 2;
            }
        }

        card.Height = Math.Max(_rowHeight, currentY);
        wrapper.Height = card.Height;
        wrapper.Controls.Add(card);
        return wrapper;
    }

    private void ReflowMessages()
    {
        var width = Math.Max(250, _messageHost.ClientSize.Width - 32);
        foreach (Control wrapper in _messageFlow.Controls)
        {
            wrapper.Width = width;

            if (wrapper.Controls.Count == 0)
            {
                continue;
            }

            var isOwn = wrapper.Tag is bool own && own;
            var card = wrapper.Controls[0];
            var cardWidth = Math.Max(220, (int)(width * 0.72));
            card.Width = cardWidth;
            card.Left = isOwn ? width - cardWidth : 0;

            foreach (Control child in card.Controls)
            {
                if (child is Label or LinkLabel)
                {
                    child.MaximumSize = new Size(cardWidth - 24, 0);
                }
            }
        }
    }
}
