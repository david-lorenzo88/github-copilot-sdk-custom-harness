using FinanceClaw.Shared;
using FinanceClaw.Tools;
using GitHub.Copilot;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 4 · Skills y agentes personalizados.
/// Skills are folders with a SKILL.md (+ references and scripts) loaded on demand.
/// The ticker researcher is a custom agent with its own prompt and a narrow tool set;
/// the main agent delegates to it as a sub-agent.
/// </summary>
public static class Step04_SkillsAndAgents
{
    public const string ResearcherName = "ticker-researcher";

    public static T Configure<T>(T config) where T : SessionConfigBase
    {
        Step03_FilesPermissionsHooks.Configure(config);

        config.SkillDirectories = [Paths.Skills];
        config.CustomAgents = [CreateResearcher()];

        // Sub-agent hooks run on the parent session.
        config.Hooks!.OnSubagentStart = (input, _) => Task.FromResult<SubagentStartHookOutput?>(new()
        {
            AdditionalContext = "Cite every number you use and say which tool it came from.",
        });

        return config;
    }

    public static CustomAgentConfig CreateResearcher(params string[] extraTools) => new()
    {
        Name = ResearcherName,
        DisplayName = "Ticker researcher",
        Description = "Researches a single ticker: price, context and a short bull/bear summary.",
        Tools = [FinanceTools.GetStockPriceName, "view", .. extraTools],
        Prompt =
            """
            You research one ticker at a time for Finance Claw.
            Return: current price, 3 bullet points of context, one bull case, one bear case.
            Use only your tools. Do not place trades and do not write files.
            """,
    };

    public static async Task RunAsync()
    {
        await using var client = await ClawClient.StartAsync();
        await using var session = await client.CreateSessionAsync(Configure(new SessionConfig()));

        await ConsoleChat.RunAsync(session, "Paso 4 · Skills y agentes");
    }
}
