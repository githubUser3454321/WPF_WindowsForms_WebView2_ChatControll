using System.Diagnostics;
using MyChat.Abstractions;

Console.WriteLine("=== MyChat Realtime Startup ===");
Console.WriteLine("Welche Sync-Technologie soll gestartet werden?");
Console.WriteLine("1 = API Polling");
Console.WriteLine("2 = SignalR");
Console.WriteLine("3 = Server Sent Events (SSE)");
Console.Write("Auswahl: ");

var key = Console.ReadLine()?.Trim();
var selectedTechnology = key switch
{
    "1" => ChatSyncTechnology.ApiPolling,
    "2" => ChatSyncTechnology.SignalR,
    "3" => ChatSyncTechnology.ServerSentEvents,
    _ => ChatSyncTechnology.SignalR
};

Console.WriteLine($"Starte Sync-Spike mit: {selectedTechnology}");

var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
var serviceProject = Path.Combine(solutionRoot, "MyChat.Sync.Service", "MyChat.Sync.Service.csproj");
var hostProject = Path.Combine(solutionRoot, "MyChat.Host.WinForms", "MyChat.Host.WinForms.csproj");
var syncUrl = "http://localhost:5088/";
const string channel = "chat-default";

var serviceProcess = StartDotNet("run", serviceProject, $"--urls={syncUrl}");
await Task.Delay(1500);

var supporterProcess = StartDotNet(
    "run",
    hostProject,
    $"--role=Supporter --displayName=Supporter --sync={selectedTechnology} --syncUrl={syncUrl} --channel={channel}");

var developerProcess = StartDotNet(
    "run",
    hostProject,
    $"--role=Applikationsentwickler --displayName=Applikationsentwickler --sync={selectedTechnology} --syncUrl={syncUrl} --channel={channel}");

Console.WriteLine("Service und zwei Host-Instanzen wurden gestartet.");
Console.WriteLine("ENTER beendet alle gestarteten Prozesse.");
Console.ReadLine();

TryKill(supporterProcess);
TryKill(developerProcess);
TryKill(serviceProcess);

static Process StartDotNet(string command, string projectPath, string args)
{
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"{command} --project \"{projectPath}\" -- {args}",
        UseShellExecute = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        CreateNoWindow = false
    };

    return Process.Start(psi)
        ?? throw new InvalidOperationException($"Prozess konnte nicht gestartet werden: {projectPath}");
}

static void TryKill(Process process)
{
    try
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }
    catch
    {
        // ignore
    }
}

static string FindSolutionRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (dir.GetFiles("*.sln").Any())
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("Konnte Solution-Root nicht ermitteln.");
}
