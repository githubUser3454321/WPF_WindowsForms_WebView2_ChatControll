using System.ComponentModel;
using System.Text.RegularExpressions;
using MyChat.Abstractions;

namespace MyChat.WinForms;

public sealed class MyChatWinFormsControl : UserControl, IMyChatBindable
{
    private readonly Panel _headerPanel;
    private readonly Label _headerLabel;
    private readonly FlowLayoutPanel _messageFlow;
    private readonly Panel _messageHost;
    private readonly RichTextBox _inputBox;
    private readonly ListBox _mentionListBox;
    private readonly Button _sendButton;
    private readonly Button _reloadButton;
    private readonly TableLayoutPanel _layout;

    private static readonly string[] MentionCandidates = ["Support", "Ich"];
    private static readonly Regex MentionRegex = new(@"(?<!\w)@([\p{L}\d_]*)", RegexOptions.Compiled);

    private int _headerHeight = 32;
    private int _rowHeight = 24;
    private readonly List<string> _pendingAttachments = [];

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

        _inputBox = new RichTextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, AcceptsTab = false, Multiline = false };
        _inputBox.TextChanged += (_, _) => HighlightMentionsAndToggleSend();
        _inputBox.KeyDown += InputBoxOnKeyDown;
        _inputBox.KeyUp += (_, _) => UpdateMentionPopup();

        _mentionListBox = new ListBox
        {
            Visible = false,
            Width = 180,
            Height = 70,
            IntegralHeight = false
        };
        _mentionListBox.Items.AddRange(MentionCandidates);
        _mentionListBox.DoubleClick += (_, _) => CommitMentionSelection();
        _mentionListBox.KeyDown += MentionListBoxOnKeyDown;
        _mentionListBox.LostFocus += (_, _) => { if (!ContainsFocus) _mentionListBox.Visible = false; };
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

        Controls.Add(_mentionListBox);
        _mentionListBox.BringToFront();
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

    public event EventHandler<ChatMessage>? MessageSubmitted;

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
        var rawText = _inputBox.Text;
        if (string.IsNullOrWhiteSpace(rawText) && _pendingAttachments.Count == 0)
        {
            return;
        }

        var user = BoundModel?.CurrentUser ?? "Ich";
        var message = new ChatMessage
        {
            Sender = user,
            Text = rawText.Trim(),
            Attachments = [.. _pendingAttachments]
        };

        AddMessage(message);
        MessageSubmitted?.Invoke(this, message);
        _pendingAttachments.Clear();
        _inputBox.Clear();
        _mentionListBox.Visible = false;
    }


    private void InputBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V)
        {
            TryPasteClipboardImage();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            SendCurrentText();
            return;
        }

        if (_mentionListBox.Visible && (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up))
        {
            FocusMentionList(e.KeyCode == Keys.Down ? 1 : -1);
            e.SuppressKeyPress = true;
            return;
        }

        if (_mentionListBox.Visible && e.KeyCode == Keys.Tab)
        {
            CommitMentionSelection();
            e.SuppressKeyPress = true;
        }
    }

    private void MentionListBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
        {
            CommitMentionSelection();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            _mentionListBox.Visible = false;
            _inputBox.Focus();
        }
    }

    private void TryPasteClipboardImage()
    {
        if (!Clipboard.ContainsImage())
        {
            return;
        }

        var attachmentName = $"ClipboardImage_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        _pendingAttachments.Add(attachmentName);
        HighlightMentionsAndToggleSend();
    }

    private void HighlightMentionsAndToggleSend()
    {
        var start = _inputBox.SelectionStart;
        var length = _inputBox.SelectionLength;

        _inputBox.SelectAll();
        _inputBox.SelectionColor = Color.Black;

        foreach (Match match in MentionRegex.Matches(_inputBox.Text))
        {
            _inputBox.Select(match.Index, match.Length);
            _inputBox.SelectionColor = Color.RoyalBlue;
        }

        _inputBox.Select(start, length);
        _sendButton.Enabled = !string.IsNullOrWhiteSpace(_inputBox.Text) || _pendingAttachments.Count > 0;
    }

    private void UpdateMentionPopup()
    {
        var caret = _inputBox.SelectionStart;
        var textBeforeCaret = _inputBox.Text[..Math.Min(caret, _inputBox.Text.Length)];
        var lastAt = textBeforeCaret.LastIndexOf('@');
        if (lastAt < 0)
        {
            _mentionListBox.Visible = false;
            return;
        }

        var query = textBeforeCaret[(lastAt + 1)..];
        if (query.Contains(' ') || query.Contains('\n'))
        {
            _mentionListBox.Visible = false;
            return;
        }

        var matches = MentionCandidates
            .Where(x => x.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            _mentionListBox.Visible = false;
            return;
        }

        _mentionListBox.BeginUpdate();
        _mentionListBox.Items.Clear();
        _mentionListBox.Items.AddRange(matches);
        _mentionListBox.SelectedIndex = 0;
        _mentionListBox.EndUpdate();

        var point = _inputBox.GetPositionFromCharIndex(lastAt);
        _mentionListBox.Left = _layout.Left + 24 + Math.Max(0, point.X);
        _mentionListBox.Top = Height - 130;
        _mentionListBox.Visible = true;
        _mentionListBox.BringToFront();
    }

    private void FocusMentionList(int delta)
    {
        if (_mentionListBox.Items.Count == 0)
        {
            return;
        }

        var next = _mentionListBox.SelectedIndex + delta;
        if (next < 0)
        {
            next = _mentionListBox.Items.Count - 1;
        }
        else if (next >= _mentionListBox.Items.Count)
        {
            next = 0;
        }

        _mentionListBox.SelectedIndex = next;
    }

    private void CommitMentionSelection()
    {
        if (!_mentionListBox.Visible || _mentionListBox.SelectedItem is not string value)
        {
            return;
        }

        var caret = _inputBox.SelectionStart;
        var text = _inputBox.Text;
        var lastAt = text[..Math.Min(caret, text.Length)].LastIndexOf('@');
        if (lastAt < 0)
        {
            return;
        }

        var suffixStart = caret;
        while (suffixStart < text.Length && !char.IsWhiteSpace(text[suffixStart]))
        {
            suffixStart++;
        }

        var replacement = $"@{value} ";
        _inputBox.Text = text[..lastAt] + replacement + text[suffixStart..];
        _inputBox.SelectionStart = lastAt + replacement.Length;
        _mentionListBox.Visible = false;
        HighlightMentionsAndToggleSend();
        _inputBox.Focus();
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
                    Location = new Point(0, currentY),
                    Tag = attachment
                };
                link.LinkClicked += (_, _) => OpenAttachment(link.Tag as string);
                card.Controls.Add(link);
                currentY = link.Bottom + 2;
            }
        }

        card.Height = Math.Max(_rowHeight, currentY);
        wrapper.Height = card.Height;
        wrapper.Controls.Add(card);
        return wrapper;
    }

    private static void OpenAttachment(string? attachment)
    {
        if (string.IsNullOrWhiteSpace(attachment))
        {
            return;
        }

        try
        {
            if (Uri.TryCreate(attachment, UriKind.Absolute, out var attachmentUri)
                && (attachmentUri.Scheme == Uri.UriSchemeHttp
                    || attachmentUri.Scheme == Uri.UriSchemeHttps
                    || attachmentUri.Scheme == Uri.UriSchemeFile))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(attachmentUri.AbsoluteUri)
                {
                    UseShellExecute = true
                });
                return;
            }

            if (File.Exists(attachment))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(attachment)
                {
                    UseShellExecute = true
                });
                return;
            }

            MessageBox.Show($"Anhang ausgewählt: {attachment}", "Anhang", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Anhang kann nicht geöffnet werden: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
