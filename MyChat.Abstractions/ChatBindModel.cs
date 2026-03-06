namespace MyChat.Abstractions;

public sealed class ChatBindModel
{
    public string ObjectType { get; set; } = string.Empty;

    public string RecordId { get; set; } = string.Empty;

    public string CurrentUser { get; set; } = string.Empty;
}
