namespace MyChat.Abstractions;

public interface IMyChatBindable
{
    int HeaderHeight { get; set; }

    int RowHeight { get; set; }

    ChatBindModel? BoundModel { get; }

    event EventHandler? ReloadRequested;

    event EventHandler<ChatMessage>? MessageSubmitted;

    void BindValues(ChatBindModel model);

    void AddMessage(ChatMessage message);
}
