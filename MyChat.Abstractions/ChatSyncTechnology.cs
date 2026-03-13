namespace MyChat.Abstractions;

public enum ChatSyncTechnology
{
    None = 0,
    ApiPolling = 1,
    SignalR = 2,
    ServerSentEvents = 3
}
