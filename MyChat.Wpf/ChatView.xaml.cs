using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MyChat.Abstractions;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MyChat.Wpf;

public partial class ChatView
{
    private static readonly string[] MentionCandidates = ["Support", "Ich"];

    private int _rowHeight = 24;
    private string _currentUser = string.Empty;
    private readonly List<string> _pendingAttachments = [];

    public ChatView()
    {
        InitializeComponent();
        SendButton.Click += (_, _) => SendCurrentText();
        InputText.TextChanged += (_, _) =>
        {
            HighlightMentions();
            UpdateMentionPopup();
            InputPlaceholder.Visibility = string.IsNullOrWhiteSpace(GetInputText()) ? Visibility.Visible : Visibility.Collapsed;
        };
        InputText.PreviewKeyDown += InputTextOnPreviewKeyDown;
        MentionList.MouseDoubleClick += (_, _) => CommitMentionSelection();
        MentionList.PreviewKeyDown += MentionListOnPreviewKeyDown;
    }

    public event EventHandler? ReloadRequested;

    public event EventHandler<ChatMessage>? MessageSubmitted;

    public void ConfigureHeader(int height, string text)
    {
        HeaderBorder.Height = Math.Max(16, height);
        HeaderText.Text = text;
    }

    public void ConfigureCurrentUser(string currentUser)
    {
        _currentUser = currentUser ?? string.Empty;
    }

    public void ConfigureRowHeight(int rowHeight)
    {
        _rowHeight = Math.Max(16, rowHeight);
    }

    public void AddMessage(ChatMessage message)
    {
        var isOwn = IsOwnMessage(message.Sender);

        var itemContainer = new Grid
        {
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = isOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 580
        };

        var itemStack = new StackPanel
        {
            MinHeight = _rowHeight
        };

        var meta = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(73, 84, 100)),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Text = $"{(isOwn ? "Ich" : message.Sender)} · {message.TimestampUtc.ToLocalTime():dd.MM.yyyy, HH:mm:ss}"
        };

        var bubble = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
            BorderBrush = new SolidColorBrush(Color.FromRgb(198, 208, 222)),
            BorderThickness = new Thickness(1),
            Background = isOwn ? new SolidColorBrush(Color.FromRgb(220, 238, 255)) : Brushes.White,
            CornerRadius = new CornerRadius(6)
        };

        var bubbleContent = new StackPanel();
        bubbleContent.Children.Add(new TextBlock
        {
            Text = message.Text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(34, 39, 46))
        });

        if (message.Attachments.Count > 0)
        {
            bubbleContent.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 6) });
            foreach (var attachment in message.Attachments)
            {
                bubbleContent.Children.Add(new Button
                {
                    Content = $"📎 {attachment}",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 4),
                    Padding = new Thickness(8, 3, 8, 3)
                });
            }
        }

        bubble.Child = bubbleContent;
        itemStack.Children.Add(meta);
        itemStack.Children.Add(bubble);
        itemContainer.Children.Add(itemStack);

        MessagePanel.Children.Add(itemContainer);
        MessageScrollViewer.ScrollToEnd();
    }

    public void SetInputPlaceholder(string text)
    {
        InputPlaceholder.Text = text;
    }

    public void SetReloadHandler()
    {
        ReloadButton.Click += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    private string GetInputText()
    {
        return new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text.TrimEnd('\r', '\n');
    }

    private void SetInputText(string text)
    {
        InputText.Document.Blocks.Clear();
        InputText.Document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0) });
        InputText.CaretPosition = InputText.Document.ContentEnd;
        HighlightMentions();
    }

    private void SendCurrentText()
    {
        var text = GetInputText();
        if (string.IsNullOrWhiteSpace(text) && _pendingAttachments.Count == 0)
        {
            return;
        }

        var message = new ChatMessage
        {
            Sender = string.IsNullOrWhiteSpace(_currentUser) ? "Ich" : _currentUser,
            Text = text.Trim(),
            Attachments = [.. _pendingAttachments]
        };

        AddMessage(message);
        MessageSubmitted?.Invoke(this, message);

        _pendingAttachments.Clear();
        SetInputText(string.Empty);
        MentionPopup.IsOpen = false;
    }

    private void InputTextOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
        {
            if (Clipboard.ContainsImage())
            {
                _pendingAttachments.Add($"ClipboardImage_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            }

            e.Handled = true;
            return;
        }

        if (MentionPopup.IsOpen && (e.Key == Key.Down || e.Key == Key.Up))
        {
            MoveMentionSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (MentionPopup.IsOpen && (e.Key == Key.Tab || e.Key == Key.Enter))
        {
            CommitMentionSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            SendCurrentText();
            e.Handled = true;
        }
    }

    private void MentionListOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            CommitMentionSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            MentionPopup.IsOpen = false;
            InputText.Focus();
        }
    }

    private void HighlightMentions()
    {
        var all = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd);
        all.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);

        var text = GetInputText();
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '@')
            {
                var end = index + 1;
                while (end < text.Length && !char.IsWhiteSpace(text[end]))
                {
                    end++;
                }

                var startPointer = GetTextPointerAtOffset(InputText.Document.ContentStart, index);
                var endPointer = GetTextPointerAtOffset(InputText.Document.ContentStart, end);
                if (startPointer is not null && endPointer is not null)
                {
                    new TextRange(startPointer, endPointer).ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.RoyalBlue);
                }

                index = end;
                continue;
            }

            index++;
        }
    }

    private static TextPointer? GetTextPointerAtOffset(TextPointer start, int offset)
    {
        var navigator = start;
        var count = 0;
        while (navigator is not null)
        {
            if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var runText = navigator.GetTextInRun(LogicalDirection.Forward);
                if (count + runText.Length >= offset)
                {
                    return navigator.GetPositionAtOffset(offset - count);
                }

                count += runText.Length;
                navigator = navigator.GetPositionAtOffset(runText.Length);
            }
            else
            {
                navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        return start;
    }

    private void UpdateMentionPopup()
    {
        var text = GetInputText();
        var caretOffset = new TextRange(InputText.Document.ContentStart, InputText.CaretPosition).Text.Length;
        var beforeCaret = text[..Math.Min(caretOffset, text.Length)];
        var at = beforeCaret.LastIndexOf('@');
        if (at < 0)
        {
            MentionPopup.IsOpen = false;
            return;
        }

        var query = beforeCaret[(at + 1)..];
        if (query.Any(char.IsWhiteSpace))
        {
            MentionPopup.IsOpen = false;
            return;
        }

        var matches = MentionCandidates.Where(x => x.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            MentionPopup.IsOpen = false;
            return;
        }

        MentionList.ItemsSource = matches;
        MentionList.SelectedIndex = 0;
        MentionPopup.HorizontalOffset = 16;
        MentionPopup.VerticalOffset = -128;
        MentionPopup.IsOpen = true;
    }

    private void MoveMentionSelection(int delta)
    {
        if (MentionList.Items.Count == 0)
        {
            return;
        }

        var next = MentionList.SelectedIndex + delta;
        if (next < 0)
        {
            next = MentionList.Items.Count - 1;
        }
        else if (next >= MentionList.Items.Count)
        {
            next = 0;
        }

        MentionList.SelectedIndex = next;
        MentionList.ScrollIntoView(MentionList.SelectedItem);
    }

    private void CommitMentionSelection()
    {
        if (!MentionPopup.IsOpen || MentionList.SelectedItem is not string selected)
        {
            return;
        }

        var text = GetInputText();
        var caretOffset = new TextRange(InputText.Document.ContentStart, InputText.CaretPosition).Text.Length;
        var beforeCaret = text[..Math.Min(caretOffset, text.Length)];
        var at = beforeCaret.LastIndexOf('@');
        if (at < 0)
        {
            return;
        }

        var end = Math.Min(caretOffset, text.Length);
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        var replacement = $"@{selected} ";
        SetInputText(text[..at] + replacement + text[end..]);
        var newPointer = GetTextPointerAtOffset(InputText.Document.ContentStart, at + replacement.Length);
        if (newPointer is not null)
        {
            InputText.CaretPosition = newPointer;
        }

        MentionPopup.IsOpen = false;
        InputText.Focus();
    }

    private bool IsOwnMessage(string sender)
    {
        if (string.Equals(sender, "Ich", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(_currentUser)
            && string.Equals(sender, _currentUser, StringComparison.OrdinalIgnoreCase);
    }
}
