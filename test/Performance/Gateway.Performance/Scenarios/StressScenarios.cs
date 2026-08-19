using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace Gateway.Performance.Scenarios;

public static class StressScenarios
{
    public static ScenarioProps CreateStressTest(int burstUsers, int durationSeconds)
    {
        var httpClient = Http.CreateDefaultClient();

        var scenario = Scenario.Create("Stress_Test", async context =>
        {
            var request = Http.CreateRequest("GET", "http://localhost:5000/users/1");
            var response = await Http.Send(httpClient, request);
            return response;
        });

        return scenario.WithLoadSimulations(
            Simulation.Inject(
                rate: burstUsers,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(durationSeconds)
            )
        );
    }
}