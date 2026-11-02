using GitHub.Copilot;

namespace FinanceClaw.Shared;

public static class ClawClient
{
    /// <summary>
    /// Model for the GitHub-hosted (subscription) steps. Null lets the runtime pick its default.
    /// </summary>
    public static string? Model => Environment.GetEnvironmentVariable("CLAW_MODEL");

    /// <summary>
    /// Starts the bundled Copilot runtime (the same engine as Copilot CLI) over stdio.
    /// Locally it reuses your logged-in Copilot user; in a container pass a token via GITHUB_TOKEN.
    /// </summary>
    public static async Task<CopilotClient> StartAsync()
    {
        var token = Environment.GetEnvironmentVariable("COPILOT_GITHUB_TOKEN")
                    ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        var client = new CopilotClient(new CopilotClientOptions
        {
            WorkingDirectory = Paths.Workspace,
            GitHubToken = string.IsNullOrEmpty(token) ? null : token,
        });

        await client.StartAsync();
        return client;
    }

    public const string Instructions =
        """
        You are Finance Claw, a personal finance assistant.
        - Use get_stock_price for prices. Never invent a price.
        - The user's holdings live in portfolio.csv in the working directory.
        - To buy or sell, use place_trade. The user will be asked to approve it.
        - Write reports as Markdown files under reports/.
        - Keep answers short and in the user's language.
        - You are not a licensed financial advisor: end any recommendation with a one-line disclaimer.
        """;
}
