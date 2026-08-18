using System.Text;
using Gateway.Performance.Infrastructure;
using Gateway.Performance.Scenarios;
using NBomber.CSharp;
using Spectre.Console;


var reportGen = new ReportGenerator();


AnsiConsole.Write(new FigletText("Performance Suite").Color(Color.Green));


var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Choose test scenario:")
        .AddChoices("Load Test", "Stress Test", "Compare with Baseline", "Exit"));


switch (choice)
{
    case "Load Test":
        await RunLoadTest();
        break;
    case "Stress Test":
        await RunStressTest();
        break;
    case "Compare with Baseline":
        await CompareWithBaseline();
        break;
    case "Exit":
        return;
}


async Task RunLoadTest()
{
    var users = AnsiConsole.Ask<int>("Number of virtual users (e.g., 100):");
    var duration = AnsiConsole.Ask<int>("Duration in seconds (e.g., 30):");


    AnsiConsole.WriteLine($"\nRunning load test with {users} users for {duration}s...\n");


    var scenario = LoadScenarios.CreateLoadTest(users, duration);
    var stats =  NBomberRunner
        .RegisterScenarios(scenario)
        .Run();  


    var nodeStats = stats.ScenarioStats.FirstOrDefault();
    var report = new PerformanceReport
    {
        Timestamp = DateTime.UtcNow,
        VirtualUsers = users,
        DurationSeconds = duration,
        TotalRequests = nodeStats?.Ok.Request.Count ?? 0,
        SuccessRate = nodeStats?.Ok.Request.Count > 0
            ? (double)nodeStats.Ok.Request.Count / (nodeStats.Ok.Request.Count + nodeStats.Fail.Request.Count) * 100
            : 0,
        AverageLatencyMs = nodeStats?.Ok.Latency.MeanMs ?? 0,
        P50 = nodeStats?.Ok.Latency.Percent50 ?? 0,
        P95 = nodeStats?.Ok.Latency.Percent95 ?? 0,
        P99 = nodeStats?.Ok.Latency.Percent99 ?? 0,
        Notes = "Load test completed"
    };


  
    var collector = new MetricsCollector();
    var metrics = await collector.CollectAsync();
    if (metrics.TryGetValue("gateway_cpu_usage", out double cpu))
        report.CpuUsage = cpu;
    if (metrics.TryGetValue("gateway_memory_usage_mb", out double mem))
        report.MemoryUsageMB = mem;


    await reportGen.SaveAsync(report, "latest.json");
    DisplayReport(report);
}


async Task RunStressTest()
{
    var burst = AnsiConsole.Ask<int>("Burst users (e.g., 500):");
    var duration = AnsiConsole.Ask<int>("Duration in seconds (e.g., 20):");


    var scenario = StressScenarios.CreateStressTest(burst, duration);
    var stats =  NBomberRunner
        .RegisterScenarios(scenario)
        .Run();  


    // Add similar reporting logic as RunLoadTest if needed
    var nodeStats = stats.ScenarioStats.FirstOrDefault();
    var report = new PerformanceReport
    {
        Timestamp = DateTime.UtcNow,
        VirtualUsers = burst,
        DurationSeconds = duration,
        TotalRequests = nodeStats?.Ok.Request.Count ?? 0,
        SuccessRate = nodeStats?.Ok.Request.Count > 0
            ? (double)nodeStats.Ok.Request.Count / (nodeStats.Ok.Request.Count + nodeStats.Fail.Request.Count) * 100
            : 0,
        AverageLatencyMs = nodeStats?.Ok.Latency.MeanMs ?? 0,
        P50 = nodeStats?.Ok.Latency.Percent50 ?? 0,
        P95 = nodeStats?.Ok.Latency.Percent95 ?? 0,
        P99 = nodeStats?.Ok.Latency.Percent99 ?? 0,
        Notes = "Stress test completed"
    };

    await reportGen.SaveAsync(report, "latest.json");
    DisplayReport(report);
}


async Task CompareWithBaseline()
{
    var baseline = await reportGen.LoadAsync("baseline.json");
    var latest = await reportGen.LoadAsync("latest.json");


    if (baseline == null || latest == null)
    {
        AnsiConsole.MarkupLine("[red]Baseline or latest report not found.[/]");
        return;
    }


    var table = new Table();
    table.AddColumn("Metric");
    table.AddColumn("Baseline");
    table.AddColumn("Latest");
    table.AddColumn("Change");


    AddRow("Requests", baseline.TotalRequests, latest.TotalRequests);
    AddRow("Success Rate", baseline.SuccessRate, latest.SuccessRate);
    AddRow("Avg Latency (ms)", baseline.AverageLatencyMs, latest.AverageLatencyMs);
    AddRow("P95 (ms)", baseline.P95, latest.P95);
    AddRow("P99 (ms)", baseline.P99, latest.P99);
    AddRow("CPU %", baseline.CpuUsage, latest.CpuUsage);
    AddRow("Memory (MB)", baseline.MemoryUsageMB, latest.MemoryUsageMB);


    AnsiConsole.Write(table);


    void AddRow(string label, double baselineVal, double latestVal)
    {
        var change = ((latestVal - baselineVal) / baselineVal) * 100;
        var color = change > 10 ? "red" : change < -10 ? "green" : "yellow";
        var arrow = change > 0 ? "▲" : "▼";
        table.AddRow(
            label,
            baselineVal.ToString("F2"),
            latestVal.ToString("F2"),
            $"[{color}]{arrow} {change:F1}%[/]"
        );
    }
}


void DisplayReport(PerformanceReport report)
{
    var panel = new Panel($@"
[bold]Performance Report[/]
Timestamp: {report.Timestamp:yyyy-MM-dd HH:mm:ss}
Virtual Users: {report.VirtualUsers}
Duration: {report.DurationSeconds}s
Total Requests: {report.TotalRequests}
Success Rate: {report.SuccessRate:F2}%
Avg Latency: {report.AverageLatencyMs:F2} ms
P50: {report.P50:F2} ms
P95: {report.P95:F2} ms
P99: {report.P99:F2} ms
CPU: {report.CpuUsage:F2}%
Memory: {report.MemoryUsageMB:F2} MB
Notes: {report.Notes}
")
    {
        Border = BoxBorder.Rounded,
        Padding = new Padding(2, 1, 2, 1)
    };
    AnsiConsole.Write(panel);
}