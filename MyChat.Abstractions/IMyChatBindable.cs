namespace MyChat.Abstractions;

public interface IMyChatBindable
{
    int HeaderHeight { get; set; }

    int RowHeight { get; set; }

    ChatBindModel? BoundModel { get; }

    event EventHandler? ReloadRequested;

    void BindValues(ChatBindModel model);

    void AddMessage(ChatMessage message);
}
