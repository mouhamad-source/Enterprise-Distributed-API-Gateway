using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using Gateway.Performance.Infrastructure;

namespace Gateway.Performance.Scenarios;

public static class LoadScenarios
{
    public static ScenarioProps CreateLoadTest(int users, int durationSeconds)
    {
        var httpClient = Http.CreateDefaultClient();
        
        var scenario = Scenario.Create("Load_Test", async context =>
        {
            var request = Http.CreateRequest("GET", "http://localhost:5000/users/1")
                .WithHeader("Accept", "application/json");
            
            var response = await Http.Send(httpClient, request);
            return response;
        });

        return scenario.WithLoadSimulations(
            Simulation.Inject(
                rate: users / durationSeconds,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(durationSeconds)
            )
        );
    }
}