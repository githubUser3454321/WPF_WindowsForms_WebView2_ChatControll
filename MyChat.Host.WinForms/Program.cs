using MyChat.Abstractions;

namespace MyChat.Host.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var options = StartupOptions.Parse(args);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(options));
    }
}

internal sealed class StartupOptions
{
    public ChatParticipantRole Role { get; init; } = ChatParticipantRole.Supporter;

    public string DisplayName { get; init; } = "Supporter";

    public ChatSyncTechnology InitialSyncTechnology { get; init; } = ChatSyncTechnology.None;

    public Uri SyncServiceUri { get; init; } = new("http://localhost:5088/");

    public string SyncChannel { get; init; } = "chat-default";

    public static StartupOptions Parse(string[] args)
    {
        var values = args
            .Select(a => a.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].TrimStart('-').ToLowerInvariant(), parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var role = values.TryGetValue("role", out var roleText)
            && Enum.TryParse<ChatParticipantRole>(roleText, ignoreCase: true, out var parsedRole)
            ? parsedRole
            : ChatParticipantRole.Supporter;

        var technology = values.TryGetValue("sync", out var syncText)
            && Enum.TryParse<ChatSyncTechnology>(syncText, ignoreCase: true, out var parsedTechnology)
            ? parsedTechnology
            : ChatSyncTechnology.None;

        var serviceUri = values.TryGetValue("syncurl", out var uriText) && Uri.TryCreate(uriText, UriKind.Absolute, out var parsedUri)
            ? parsedUri
            : new Uri("http://localhost:5088/");

        var displayName = values.TryGetValue("displayname", out var customName)
            ? customName
            : (role == ChatParticipantRole.Applikationsentwickler ? "Applikationsentwickler" : "Supporter");

        var channel = values.TryGetValue("channel", out var channelValue)
            ? channelValue
            : "chat-default";

        return new StartupOptions
        {
            Role = role,
            DisplayName = displayName,
            InitialSyncTechnology = technology,
            SyncServiceUri = serviceUri,
            SyncChannel = channel
        };
    }
}
