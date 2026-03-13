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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"api/messages/stream?channel={Uri.EscapeDataString(channel)}");
                request.Headers.Accept.ParseAdd("text/event-stream");

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line is null)
                    {
                        break;
                    }

                    if (!line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var payload = line[5..].TrimStart();
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        continue;
                    }

                    var message = JsonSerializer.Deserialize<ChatSyncMessageDto>(payload);
                    if (message is not null && message.Channel == channel)
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(500, cancellationToken);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
