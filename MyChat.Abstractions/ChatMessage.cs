namespace MyChat.Abstractions;

public sealed class ChatMessage
{
    public string Sender { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public List<string> Attachments { get; set; } = [];
}
