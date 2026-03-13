using System.Net.Http.Json;
using System.Text.Json;

namespace MyChat.Host.WinForms.Sync;

internal sealed class SseChatSyncClient(HttpClient httpClient, string channel) : IChatSyncClient
{
    private CancellationTokenSource? _cts;

    public event EventHandler<ChatSyncMessageDto>? MessageReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(ChatSyncMessageDto message, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/messages", message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await httpClient.GetStreamAsync($"api/messages/stream?channel={Uri.EscapeDataString(channel)}", cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    continue;
                }

                var payload = line[6..];
                var message = JsonSerializer.Deserialize<ChatSyncMessageDto>(payload);
                if (message is not null && message.Channel == channel)
                {
                    MessageReceived?.Invoke(this, message);
                }
            }
        }
        catch
        {
            // Spike: Fehler werden bewusst toleriert.
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
