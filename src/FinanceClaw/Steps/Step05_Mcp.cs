using FinanceClaw.Shared;
using GitHub.Copilot;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 5 · Servidores MCP para datos externos.
/// FinanceClaw.MarketMcp is a stdio MCP server (news, company profiles, FX).
/// Runtime tool names are "&lt;server-key&gt;-&lt;tool-name&gt;", e.g. market-get_market_news.
/// </summary>
public static class Step05_Mcp
{
    public const string ServerKey = "market";

    public static T Configure<T>(T config) where T : SessionConfigBase
    {
        Step04_SkillsAndAgents.Configure(config);

        config.McpServers = new Dictionary<string, McpServerConfig>
        {
            [ServerKey] = new McpStdioServerConfig
            {
                Command = "dotnet",
                Args = [Paths.MarketMcpDll],
                Tools = ["*"],
            },
        };

        // The researcher now gets the market data tools too.
        config.CustomAgents =
        [
            Step04_SkillsAndAgents.CreateResearcher(
                $"{ServerKey}-get_market_news",
                $"{ServerKey}-get_company_profile"),
        ];

        return config;
    }

    public static async Task RunAsync()
    {
        await using var client = await ClawClient.StartAsync();
        await using var session = await client.CreateSessionAsync(Configure(new SessionConfig()));

        await ConsoleChat.RunAsync(session, "Paso 5 · MCP");
    }
}
