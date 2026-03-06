using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyChat.Abstractions;

namespace MyChat.Wpf;

public partial class ChatView : UserControl
{
    private int _rowHeight = 24;
    private string _currentUser = string.Empty;

    public ChatView()
    {
        InitializeComponent();
        SendButton.Click += (_, _) => SendCurrentText();
    }

    public event EventHandler? ReloadRequested;

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
        InputText.ToolTip = text;
    }

    public void SetReloadHandler()
    {
        ReloadButton.Click += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    public string ConsumeInputText()
    {
        var value = InputText.Text;
        InputText.Clear();
        return value;
    }

    private void SendCurrentText()
    {
        var text = ConsumeInputText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            AddMessage(new ChatMessage { Sender = string.IsNullOrWhiteSpace(_currentUser) ? "Ich" : _currentUser, Text = text.Trim() });
        }
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
