using MyChat.Abstractions;

namespace MyChat.Host.WinForms;

internal static class Program
{
    internal static readonly ChatMemoryStore Memory = new();

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class ChatMemoryStore
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _messages = [];

    public ChatBindModel Model { get; } = new()
    {
        ObjectType = "Invoice",
        RecordId = "123",
        CurrentUser = "Matthias"
    };

    public IReadOnlyList<ChatMessage> SnapshotMessages()
    {
        lock (_gate)
        {
            return _messages
                .Select(x => new ChatMessage
                {
                    Sender = x.Sender,
                    Text = x.Text,
                    TimestampUtc = x.TimestampUtc,
                    Attachments = [.. x.Attachments]
                })
                .ToList();
        }
    }

    public void AddMessage(ChatMessage message)
    {
        lock (_gate)
        {
            _messages.Add(new ChatMessage
            {
                Sender = message.Sender,
                Text = message.Text,
                TimestampUtc = message.TimestampUtc,
                Attachments = [.. message.Attachments]
            });
        }
    }
}
