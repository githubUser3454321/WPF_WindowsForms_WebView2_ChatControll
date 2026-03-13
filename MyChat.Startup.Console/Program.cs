using System.Diagnostics;
using System.Net.Http.Json;
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

var solutionFile = Path.Combine(solutionRoot, "MyChat.sln");

RestoreSolution(solutionFile);
BuildProject(serviceProject);
BuildProject(hostProject);

var serviceProcess = StartDotNetRun(serviceProject, $"--urls={syncUrl}");
await WaitForServiceReadyAsync(syncUrl);

var supporterProcess = StartDotNetRun(
    hostProject,
    $"--role=Supporter --displayName=Supporter --sync={selectedTechnology} --syncUrl={syncUrl} --channel={channel}");

var developerProcess = StartDotNetRun(
    hostProject,
    $"--role=Applikationsentwickler --displayName=Applikationsentwickler --sync={selectedTechnology} --syncUrl={syncUrl} --channel={channel}");

Console.WriteLine("Service und zwei Host-Instanzen wurden gestartet.");
Console.WriteLine("ENTER beendet alle gestarteten Prozesse.");
Console.ReadLine();

TryKill(supporterProcess);
TryKill(developerProcess);
TryKill(serviceProcess);

static void RestoreSolution(string solutionPath)
{
    var restore = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"restore --nologo \"{solutionPath}\"",
        UseShellExecute = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        CreateNoWindow = false
    };

    using var process = Process.Start(restore)
        ?? throw new InvalidOperationException($"Restore konnte nicht gestartet werden: {solutionPath}");

    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Restore fehlgeschlagen für: {solutionPath}");
    }
}

static void BuildProject(string projectPath)
{
    var build = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"build --no-restore --nologo --verbosity minimal \"{projectPath}\"",
        UseShellExecute = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        CreateNoWindow = false
    };

    using var process = Process.Start(build)
        ?? throw new InvalidOperationException($"Build konnte nicht gestartet werden: {projectPath}");

    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Build fehlgeschlagen für: {projectPath}");
    }
}

static Process StartDotNetRun(string projectPath, string args)
{
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --no-build --no-restore --project \"{projectPath}\" -- {args}",
        UseShellExecute = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        CreateNoWindow = false
    };

    return Process.Start(psi)
        ?? throw new InvalidOperationException($"Prozess konnte nicht gestartet werden: {projectPath}");
}

static async Task WaitForServiceReadyAsync(string baseUrl)
{
    using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

    for (var attempt = 0; attempt < 30; attempt++)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<Dictionary<string, string>>("health");
            if (response is not null && response.TryGetValue("status", out var status) && status == "ok")
            {
                return;
            }
        }
        catch
        {
            // service is still booting
        }

        await Task.Delay(250);
    }

    throw new TimeoutException("Sync-Service konnte nicht rechtzeitig gestartet werden.");
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
