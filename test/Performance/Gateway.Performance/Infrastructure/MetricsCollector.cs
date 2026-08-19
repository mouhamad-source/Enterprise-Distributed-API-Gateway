using System.Net.Http;
using System.Text.RegularExpressions;

namespace Gateway.Performance.Infrastructure;

public class MetricsCollector
{
    private readonly HttpClient _client;
    private readonly string _gatewayUrl;

    public MetricsCollector(string gatewayUrl = "http://localhost:5000")
    {
        _gatewayUrl = gatewayUrl;
        _client = new HttpClient();
    }

    public async Task<Dictionary<string, double>> CollectAsync()
    {
        var result = new Dictionary<string, double>();
        try
        {
            var response = await _client.GetAsync($"{_gatewayUrl}/metrics");
            if (response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();
                
                var lines = text.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("gateway_") && !line.Contains("{"))
                    {
                        var parts = line.Split(' ');
                        if (parts.Length == 2 && double.TryParse(parts[1], out double value))
                        {
                            result[parts[0]] = value;
                        }
                    }
                }
            }
        }
        catch {  }
        return result;
    }
}