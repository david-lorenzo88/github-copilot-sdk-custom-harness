using FinanceClaw.Shared;
using GitHub.Copilot;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 6 · Sesiones que se guardan y se reanudan.
/// Session state is persisted by the runtime (~/.copilot/session-state/{id}); ResumeSessionAsync
/// picks it up again. Infinite sessions compact the context in the background.
/// Handlers, tools and hooks live in this process, so they are re-supplied on resume.
/// </summary>
public static class Step06_Sessions
{
    public static T Configure<T>(T config) where T : SessionConfigBase
    {
        Step05_Mcp.Configure(config);

        config.InfiniteSessions = new InfiniteSessionConfig
        {
            Enabled = true,
            BackgroundCompactionThreshold = 0.50, // low on purpose, to see it during the demo
            BufferExhaustionThreshold = 0.90,
        };
        return config;
    }

    public static async Task RunAsync()
    {
        await using var client = await ClawClient.StartAsync();

        var previous = (await client.ListSessionsAsync())
            .Where(s => s.SessionId.StartsWith("claw-", StringComparison.Ordinal))
            .OrderByDescending(s => s.ModifiedTime)
            .Take(5)
            .ToList();

        Ui.Header("Sesiones anteriores");
        for (var i = 0; i < previous.Count; i++)
            Console.WriteLine($"  {i + 1}. {previous[i].SessionId}  {previous[i].ModifiedTime:g}  {Ui.Truncate(previous[i].Summary, 60)}");
        Console.Write("  Número para reanudar (Enter = nueva): ");

        await using var session =
            int.TryParse(Console.ReadLine(), out var pick) && pick >= 1 && pick <= previous.Count
                ? await client.ResumeSessionAsync(previous[pick - 1].SessionId, Configure(new ResumeSessionConfig()))
                : await client.CreateSessionAsync(Configure(new SessionConfig
                {
                    SessionId = $"claw-{DateTime.Now:yyyyMMdd-HHmmss}",
                }));

        Ui.Dim($"workspace de la sesión: {session.WorkspacePath}");
        await ConsoleChat.RunAsync(session, "Paso 6 · Sesiones persistentes e infinitas");
    }
}
