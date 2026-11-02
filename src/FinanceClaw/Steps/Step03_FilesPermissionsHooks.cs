using FinanceClaw.Shared;
using GitHub.Copilot;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 3 · Tu cartera en ficheros.
/// Copilot's built-in tools (read, edit, shell…) work on workspace/, under two layers of control:
///  - OnPermissionRequest: may this tool run? (reads inside the repo, writes inside workspace/, shell asks)
///  - Hooks: policy and audit around every call (portfolio.csv is read-only, audit.log, trade confirmations)
/// </summary>
public static class Step03_FilesPermissionsHooks
{
    public static T Configure<T>(T config) where T : SessionConfigBase
    {
        Step02_CustomTools.Configure(config);

        config.WorkingDirectory = Paths.Workspace;
        config.OnPermissionRequest = Approvals.Portfolio;
        config.Hooks = AuditHooks.Create();
        return config;
    }

    public static async Task RunAsync()
    {
        await using var client = await ClawClient.StartAsync();
        await using var session = await client.CreateSessionAsync(Configure(new SessionConfig()));

        await ConsoleChat.RunAsync(session, "Paso 3 · Cartera en ficheros, permisos y hooks");
    }
}
