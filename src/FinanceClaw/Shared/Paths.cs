namespace FinanceClaw.Shared;

/// <summary>Resolves the repo folders (workspace, skills, MCP server) no matter where the app runs from.</summary>
public static class Paths
{
    public static string Root { get; } = FindRoot();

    /// <summary>The claw's working directory: portfolio.csv, reports/, confirmations/.</summary>
    public static string Workspace => Path.Combine(Root, "workspace");

    public static string Skills => Path.Combine(Root, "skills");

    public static string AuditLog => Path.Combine(Workspace, "audit.log");

    public static string MarketMcpDll =>
        Environment.GetEnvironmentVariable("MARKET_MCP_DLL")
        ?? Path.Combine(Root, "src", "FinanceClaw.MarketMcp", "bin", "Debug", "net10.0", "FinanceClaw.MarketMcp.dll");

    private static string FindRoot()
    {
        if (Environment.GetEnvironmentVariable("CLAW_ROOT") is { Length: > 0 } fromEnv)
            return fromEnv;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "workspace")) &&
                Directory.Exists(Path.Combine(dir.FullName, "skills")))
                return dir.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
