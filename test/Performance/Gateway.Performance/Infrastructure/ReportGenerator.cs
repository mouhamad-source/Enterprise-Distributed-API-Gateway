using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gateway.Performance.Infrastructure;

public class PerformanceReport
{
    public DateTime Timestamp { get; set; }
    public string CommitHash { get; set; } = "";
    public string MachineSpecs { get; set; } = "";
    public int VirtualUsers { get; set; }
    public double DurationSeconds { get; set; }
    public int TotalRequests { get; set; }
    public double SuccessRate { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsageMB { get; set; }
    public string Notes { get; set; } = "";
}

public class ReportGenerator
{
    private readonly string _outputDir;

    public ReportGenerator(string outputDir = "./Reports")
    {
        _outputDir = outputDir;
        Directory.CreateDirectory(outputDir);
    }

    public async Task SaveAsync(PerformanceReport report, string filename = "latest.json")
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(_outputDir, filename);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<PerformanceReport> LoadAsync(string filename = "baseline.json")
    {
        var path = Path.Combine(_outputDir, filename);
        if (!File.Exists(path))
            return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<PerformanceReport>(json);
    }
}