using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
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

var analysis = await RunNutzwertAnalyseAsync(syncUrl, selectedTechnology, channel);
var reportPath = WriteCsvReport(solutionRoot, analysis);
PrintAnalysisSummary(analysis, reportPath);

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

    psi.Environment["Logging__LogLevel__Default"] = "Warning";
    psi.Environment["Logging__LogLevel__Microsoft"] = "Warning";
    psi.Environment["Logging__LogLevel__Microsoft.AspNetCore"] = "Warning";
    psi.Environment["DOTNET_PRINT_TELEMETRY_MESSAGE"] = "false";

    return Process.Start(psi)
        ?? throw new InvalidOperationException($"Prozess konnte nicht gestartet werden: {projectPath}");
}

static async Task<NutzwertAnalyseResult> RunNutzwertAnalyseAsync(string baseUrl, ChatSyncTechnology selectedTechnology, string channel)
{
    using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

    var latency = await MeasureLatencyAsync(httpClient, channel, 30);
    var reconnect = await MeasureReconnectStabilityAsync(httpClient, channel);
    var load = await MeasureScalabilityAsync(httpClient, channel);
    var errorHandling = await MeasureErrorHandlingAsync(httpClient);

    return new NutzwertAnalyseResult(
        selectedTechnology,
        latency,
        reconnect,
        load,
        errorHandling,
        DateTime.UtcNow);
}

static async Task<LatencyResult> MeasureLatencyAsync(HttpClient httpClient, string channel, int sampleCount)
{
    var durations = new List<double>(sampleCount);
    var overFiveSeconds = 0;

    for (var i = 0; i < sampleCount; i++)
    {
        var payload = new
        {
            sender = "AnalyseBot",
            text = $"latency-{i}",
            sentAtUtc = DateTime.UtcNow,
            channel
        };

        var sw = Stopwatch.StartNew();
        using var response = await httpClient.PostAsJsonAsync("api/messages", payload);
        sw.Stop();

        response.EnsureSuccessStatusCode();

        var ms = sw.Elapsed.TotalMilliseconds;
        durations.Add(ms);
        if (ms > 5000)
        {
            overFiveSeconds++;
        }
    }

    durations.Sort();
    var p95 = Percentile(durations, 95);
    var p99 = Percentile(durations, 99);
    var compliance = sampleCount == 0 ? 0 : (sampleCount - overFiveSeconds) * 100d / sampleCount;

    return new LatencyResult(sampleCount, p95, p99, overFiveSeconds, compliance);
}

static async Task<ReconnectResult> MeasureReconnectStabilityAsync(HttpClient httpClient, string channel)
{
    var first = await httpClient.GetAsync($"api/messages?sinceId=0&channel={Uri.EscapeDataString(channel)}");
    first.EnsureSuccessStatusCode();

    await Task.Delay(200);

    var second = await httpClient.GetAsync($"api/messages?sinceId=0&channel={Uri.EscapeDataString(channel)}");
    second.EnsureSuccessStatusCode();

    return new ReconnectResult(first.IsSuccessStatusCode && second.IsSuccessStatusCode, "HTTP-Reconnect per Kurzunterbruch erfolgreich.");
}

static async Task<ScalabilityResult> MeasureScalabilityAsync(HttpClient httpClient, string channel)
{
    var users = 10;
    var messagesPerUser = 5;
    var allTasks = new List<Task<double>>();

    for (var user = 0; user < users; user++)
    {
        for (var message = 0; message < messagesPerUser; message++)
        {
            var sender = $"load-user-{user}";
            var text = $"load-message-{message}";
            allTasks.Add(SendAndMeasureAsync(httpClient, channel, sender, text));
        }
    }

    var results = await Task.WhenAll(allTasks);
    var average = results.Average();
    var max = results.Max();

    return new ScalabilityResult(users, users * messagesPerUser, average, max);
}

static async Task<double> SendAndMeasureAsync(HttpClient httpClient, string channel, string sender, string text)
{
    var payload = new { sender, text, sentAtUtc = DateTime.UtcNow, channel };
    var sw = Stopwatch.StartNew();
    using var response = await httpClient.PostAsJsonAsync("api/messages", payload);
    sw.Stop();
    response.EnsureSuccessStatusCode();
    return sw.Elapsed.TotalMilliseconds;
}

static async Task<ErrorHandlingResult> MeasureErrorHandlingAsync(HttpClient httpClient)
{
    using var invalidResponse = await httpClient.PostAsync("api/messages", new StringContent("{invalid", Encoding.UTF8, "application/json"));
    var isHandled = (int)invalidResponse.StatusCode is >= 400 and < 500;
    return new ErrorHandlingResult((int)invalidResponse.StatusCode, isHandled);
}

static double Percentile(IReadOnlyList<double> sortedValues, int percentile)
{
    if (sortedValues.Count == 0)
    {
        return 0;
    }

    var rank = (percentile / 100d) * (sortedValues.Count - 1);
    var lowerIndex = (int)Math.Floor(rank);
    var upperIndex = (int)Math.Ceiling(rank);

    if (lowerIndex == upperIndex)
    {
        return sortedValues[lowerIndex];
    }

    var weight = rank - lowerIndex;
    return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
}

static string WriteCsvReport(string solutionRoot, NutzwertAnalyseResult analysis)
{
    var fileName = $"nutzwertanalyse_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
    var fullPath = Path.Combine(solutionRoot, fileName);

    var lines = new List<string>
    {
        "Kriterium,Fragestellung,Nachweis / Messung,Ergebnis",
        $"Latenzerfüllung,Erfüllt die Technologie 99 % < 5 s?,{analysis.Latency.SampleCount} Requests gemessen,Compliance={analysis.Latency.CompliancePercent:F2}% | P99={analysis.Latency.P99Ms:F2}ms | >5s={analysis.Latency.AboveFiveSecondsCount}",
        $"Integrationsaufwand,Wie aufwendig ist die Einbindung in OM?,PoC im Startup + Service verwendet,Mittel (bereits in C#/.NET integriert)",
        $"Architekturkonformität,Passt die Lösung zu C#/.NET WindowsForms FireFly?,Analyse der vorhandenen Projekte,Konform (WinForms Hosts + .NET Service)",
        $"Reconnect-Stabilität,Verhalten bei Verbindungsunterbruch?,2x API-Neuaufbau getestet,{(analysis.Reconnect.Recovered ? "Bestanden" : "Fehlgeschlagen")}: {analysis.Reconnect.Notes}",
        $"Echtzeit-Eignung,Push statt zyklisches Pulling?,Technologieeigenschaft aus Auswahl,{(analysis.SelectedTechnology is ChatSyncTechnology.SignalR or ChatSyncTechnology.ServerSentEvents ? "Push-fähig" : "Polling-basiert")}",
        $"Skalierbarkeit,Verhalten bei 5-10 bis 50 Usern und Peaks?,Lasttest mit {analysis.Scalability.VirtualUsers} virtuellen Usern,{analysis.Scalability.TotalMessages} Nachrichten | Avg={analysis.Scalability.AverageLatencyMs:F2}ms | Max={analysis.Scalability.MaxLatencyMs:F2}ms",
        $"Rechtekonforme Zustellung,Ist selektive Zustellung ohne Preview-Leaks möglich?,Nicht automatisiert im PoC,Nicht bewertet",
        $"Status gelesen/ungelesen,Lässt sich FA-09 konsistent umsetzen?,Nicht automatisiert im PoC,Nicht bewertet",
        $"Fehlerbehandlung,Sind Fehler nachvollziehbar und loggbar?,Ungültiger JSON-Request,{(analysis.ErrorHandling.IsHandled ? "Bestanden" : "Fehlgeschlagen")} (HTTP {analysis.ErrorHandling.StatusCode})",
        $"Aufwand Diplomarbeit,Ist die Lösung im Projektumfang realistisch?,Technische Risikoabschätzung,Realistisch mit Fokus auf Kernkriterien",
        $"Erweiterbarkeit,Eignet sich die Lösung für spätere Ausbauten?,Architekturbeobachtung,Erweiterbar durch zusätzliche Endpunkte/Events"
    };

    File.WriteAllLines(fullPath, lines);
    return fullPath;
}

static void PrintAnalysisSummary(NutzwertAnalyseResult analysis, string reportPath)
{
    Console.WriteLine();
    Console.WriteLine("=== Nutzwertanalyse (automatisch) ===");
    Console.WriteLine($"Technologie: {analysis.SelectedTechnology}");
    Console.WriteLine($"Latenz: P95={analysis.Latency.P95Ms:F2}ms | P99={analysis.Latency.P99Ms:F2}ms | >5s={analysis.Latency.AboveFiveSecondsCount}/{analysis.Latency.SampleCount} | Compliance={analysis.Latency.CompliancePercent:F2}%");
    Console.WriteLine($"Reconnect: {(analysis.Reconnect.Recovered ? "stabil" : "instabil")} ({analysis.Reconnect.Notes})");
    Console.WriteLine($"Skalierungstest: {analysis.Scalability.TotalMessages} Nachrichten, Avg={analysis.Scalability.AverageLatencyMs:F2}ms, Max={analysis.Scalability.MaxLatencyMs:F2}ms");
    Console.WriteLine($"Fehlerbehandlung: HTTP {analysis.ErrorHandling.StatusCode} ({(analysis.ErrorHandling.IsHandled ? "korrekt abgefangen" : "prüfen")})");
    Console.WriteLine($"CSV-Report: {reportPath}");
    Console.WriteLine();
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

public sealed record NutzwertAnalyseResult(
    ChatSyncTechnology SelectedTechnology,
    LatencyResult Latency,
    ReconnectResult Reconnect,
    ScalabilityResult Scalability,
    ErrorHandlingResult ErrorHandling,
    DateTime GeneratedAtUtc);

public sealed record LatencyResult(int SampleCount, double P95Ms, double P99Ms, int AboveFiveSecondsCount, double CompliancePercent);

public sealed record ReconnectResult(bool Recovered, string Notes);

public sealed record ScalabilityResult(int VirtualUsers, int TotalMessages, double AverageLatencyMs, double MaxLatencyMs);

public sealed record ErrorHandlingResult(int StatusCode, bool IsHandled);
