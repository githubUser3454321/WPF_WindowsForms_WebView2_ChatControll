using System.Windows;
using System.Windows.Controls;
using MyChat.Abstractions;

namespace MyChat.Wpf;

public partial class ChatView : UserControl
{
    private int _rowHeight = 24;

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

    public void ConfigureRowHeight(int rowHeight)
    {
        _rowHeight = Math.Max(16, rowHeight);
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(HeightProperty, (double)_rowHeight));
        MessageList.ItemContainerStyle = style;
    }

    public void AddMessage(ChatMessage message)
    {
        MessageList.Items.Add($"[{message.TimestampUtc:HH:mm}] {message.Sender}: {message.Text}");
        if (MessageList.Items.Count > 0)
        {
            MessageList.ScrollIntoView(MessageList.Items[^1]);
        }
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
            AddMessage(new ChatMessage { Sender = "User", Text = text.Trim() });
        }
    }
}
