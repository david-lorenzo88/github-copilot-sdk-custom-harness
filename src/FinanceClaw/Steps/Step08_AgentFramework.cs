using FinanceClaw.Shared;
using GitHub.Copilot;
using Microsoft.Agents.AI;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 8 · El mismo agente desde Microsoft Agent Framework.
/// AsAIAgent wraps the Copilot session as an AIAgent, so the claw plugs into anything that
/// speaks Agent Framework (workflows, hosting, A2A…) while Copilot still runs the agentic loop.
/// </summary>
public static class Step08_AgentFramework
{
    public static async Task RunAsync()
    {
        var client = await ClawClient.StartAsync();

        AIAgent agent = client.AsAIAgent(
            Step05_Mcp.Configure(new SessionConfig()),
            ownsClient: true,
            name: "FinanceClaw",
            description: "Personal finance assistant running on the GitHub Copilot engine");

        var session = await agent.CreateSessionAsync();

        Ui.Header("Paso 8 · Microsoft Agent Framework (AIAgent)");
        Ui.Dim($"agent: {agent.Name} ({agent.GetType().Name})  ·  escribe 'exit' para salir");

        while (true)
        {
            Console.Write("\n> ");
            var prompt = Console.ReadLine();
            if (prompt is null || prompt.Trim() is "exit" or "quit")
                break;
            if (string.IsNullOrWhiteSpace(prompt))
                continue;

            await foreach (var update in agent.RunStreamingAsync(prompt, session))
                Console.Write(update);
            Console.WriteLine();
        }

        if (agent is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }
}
