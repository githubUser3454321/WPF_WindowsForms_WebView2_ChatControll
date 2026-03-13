using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace MyChat.Host.WinForms.Sync;

internal sealed class SignalRChatSyncClient(Uri baseUri, string channel) : IChatSyncClient
{
    private readonly HttpClient _httpClient = new() { BaseAddress = baseUri };
    private readonly HubConnection _connection = new HubConnectionBuilder()
        .WithUrl(new Uri(baseUri, "hubs/chat"))
        .WithAutomaticReconnect()
        .Build();

    public event EventHandler<ChatSyncMessageDto>? MessageReceived;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection.On<ChatSyncMessageDto>("message", message =>
        {
            if (message.Channel == channel)
            {
                MessageReceived?.Invoke(this, message);
            }
        });

        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("JoinChannel", channel, cancellationToken);
    }

    public async Task PublishAsync(ChatSyncMessageDto message, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("api/messages", message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _httpClient.Dispose();
    }
}
