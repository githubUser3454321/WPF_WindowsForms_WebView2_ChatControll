using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using MyChat.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<SyncMessageStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/messages", (SyncMessageStore store, long? sinceId) =>
{
    var messages = store.GetSince(sinceId ?? 0L);
    return Results.Ok(messages);
});

app.MapPost("/api/messages", async (ChatSyncMessage message, SyncMessageStore store, IHubContext<ChatSyncHub> hub, CancellationToken cancellationToken) =>
{
    var saved = store.Append(message);
    await hub.Clients.Group(saved.Channel).SendAsync("message", saved, cancellationToken);
    return Results.Accepted($"/api/messages/{saved.Id}", saved);
});

app.MapGet("/api/messages/stream", async (HttpContext context, SyncMessageStore store, string? channel, CancellationToken cancellationToken) =>
{
    context.Response.Headers.ContentType = "text/event-stream";

    await foreach (var message in store.Stream(channel ?? SyncMessageStore.DefaultChannel, cancellationToken))
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(message);
        await context.Response.WriteAsync($"event: message\n", cancellationToken);
        await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }
});

app.MapHub<ChatSyncHub>("/hubs/chat");

app.Run();

public sealed class ChatSyncHub : Hub
{
    public Task JoinChannel(string channel)
    {
        var selectedChannel = string.IsNullOrWhiteSpace(channel) ? SyncMessageStore.DefaultChannel : channel;
        return Groups.AddToGroupAsync(Context.ConnectionId, selectedChannel);
    }
}

public sealed class SyncMessageStore
{
    public const string DefaultChannel = "chat-default";

    private readonly object _gate = new();
    private readonly List<ChatSyncMessage> _messages = [];
    private readonly ConcurrentDictionary<string, Channel<ChatSyncMessage>> _channels = new();
    private long _idCounter;

    public ChatSyncMessage Append(ChatSyncMessage input)
    {
        var saved = new ChatSyncMessage
        {
            Id = Interlocked.Increment(ref _idCounter),
            Sender = input.Sender,
            Text = input.Text,
            SentAtUtc = input.SentAtUtc == default ? DateTime.UtcNow : input.SentAtUtc,
            Channel = string.IsNullOrWhiteSpace(input.Channel) ? DefaultChannel : input.Channel
        };

        lock (_gate)
        {
            _messages.Add(saved);
        }

        var writerChannel = _channels.GetOrAdd(saved.Channel, _ => Channel.CreateUnbounded<ChatSyncMessage>());
        writerChannel.Writer.TryWrite(saved);
        return saved;
    }

    public IReadOnlyList<ChatSyncMessage> GetSince(long sinceId)
    {
        lock (_gate)
        {
            return _messages
                .Where(x => x.Id > sinceId)
                .Select(Clone)
                .ToList();
        }
    }

    public async IAsyncEnumerable<ChatSyncMessage> Stream(string channel, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var selectedChannel = string.IsNullOrWhiteSpace(channel) ? DefaultChannel : channel;
        var streamChannel = _channels.GetOrAdd(selectedChannel, _ => Channel.CreateUnbounded<ChatSyncMessage>());

        while (await streamChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (streamChannel.Reader.TryRead(out var message))
            {
                yield return Clone(message);
            }
        }
    }

    private static ChatSyncMessage Clone(ChatSyncMessage input)
    {
        return new ChatSyncMessage
        {
            Id = input.Id,
            Sender = input.Sender,
            Text = input.Text,
            SentAtUtc = input.SentAtUtc,
            Channel = input.Channel
        };
    }
}

public sealed class ChatSyncMessage
{
    public long Id { get; set; }

    public string Sender { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    public string Channel { get; set; } = SyncMessageStore.DefaultChannel;
}
