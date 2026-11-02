using FinanceClaw.Shared;
using GitHub.Copilot;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 1 · Hola, motor de Copilot.
/// A CopilotClient starts the bundled runtime; a session is one agentic conversation.
/// </summary>
public static class Step01_Hello
{
    public static T Configure<T>(T config) where T : SessionConfigBase
    {
        config.Model = ClawClient.Model;
        config.Streaming = true;
        config.SystemMessage = new SystemMessageConfig
        {
            Mode = SystemMessageMode.Append,
            Content = ClawClient.Instructions,
        };
        config.OnPermissionRequest = Approvals.DenyAll;
        return config;
    }

    public static async Task RunAsync()
    {
        await using var client = await ClawClient.StartAsync();
        await using var session = await client.CreateSessionAsync(Configure(new SessionConfig()));

        await ConsoleChat.RunAsync(session, "Paso 1 · Hola Copilot SDK");
    }
}
