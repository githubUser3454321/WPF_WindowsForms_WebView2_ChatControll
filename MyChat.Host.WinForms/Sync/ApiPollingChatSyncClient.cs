using System.Net.Http.Json;

namespace MyChat.Host.WinForms.Sync;

internal sealed class ApiPollingChatSyncClient(HttpClient httpClient, string channel) : IChatSyncClient
{
    private long _lastSeenId;
    private CancellationTokenSource? _cts;

    public event EventHandler<ChatSyncMessageDto>? MessageReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(ChatSyncMessageDto message, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/messages", message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await httpClient.GetFromJsonAsync<List<ChatSyncMessageDto>>($"api/messages?sinceId={_lastSeenId}", cancellationToken)
                    ?? [];

                foreach (var message in messages.Where(m => m.Channel == channel && m.Id > _lastSeenId).OrderBy(m => m.Id))
                {
                    _lastSeenId = message.Id;
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch
            {
                // Spike: Fehler werden bewusst toleriert.
            }

            await Task.Delay(400, cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
