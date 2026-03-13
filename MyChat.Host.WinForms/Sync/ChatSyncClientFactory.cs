using MyChat.Abstractions;

namespace MyChat.Host.WinForms.Sync;

internal static class ChatSyncClientFactory
{
    public static IChatSyncClient Create(ChatSyncTechnology technology, Uri serviceUri, string channel)
    {
        if (technology == ChatSyncTechnology.None)
        {
            return new NullChatSyncClient();
        }

        var httpClient = new HttpClient
        {
            BaseAddress = serviceUri
        };

        return technology switch
        {
            ChatSyncTechnology.ApiPolling => new ApiPollingChatSyncClient(httpClient, channel),
            ChatSyncTechnology.SignalR => new SignalRChatSyncClient(serviceUri, channel),
            ChatSyncTechnology.ServerSentEvents => new SseChatSyncClient(httpClient, channel),
            _ => new NullChatSyncClient()
        };
    }
}
