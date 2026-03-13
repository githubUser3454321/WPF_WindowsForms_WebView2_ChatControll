namespace MyChat.Host.WinForms.Sync;

internal sealed class NullChatSyncClient : IChatSyncClient
{
    public event EventHandler<ChatSyncMessageDto>? MessageReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync(ChatSyncMessageDto message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
