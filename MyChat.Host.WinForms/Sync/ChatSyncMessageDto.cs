namespace MyChat.Host.WinForms.Sync;

internal sealed class ChatSyncMessageDto
{
    public long Id { get; set; }

    public string Sender { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }

    public string Channel { get; set; } = "chat-default";
}
