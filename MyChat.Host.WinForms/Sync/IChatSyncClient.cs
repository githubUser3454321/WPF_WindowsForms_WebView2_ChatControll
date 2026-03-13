namespace MyChat.Host.WinForms.Sync;

internal interface IChatSyncClient : IAsyncDisposable
{
    event EventHandler<ChatSyncMessageDto>? MessageReceived;

    Task StartAsync(CancellationToken cancellationToken);

    Task PublishAsync(ChatSyncMessageDto message, CancellationToken cancellationToken);
}
