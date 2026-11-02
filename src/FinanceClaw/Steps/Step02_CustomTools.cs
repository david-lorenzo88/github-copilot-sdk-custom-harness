using FinanceClaw.Shared;
using FinanceClaw.Tools;
using GitHub.Copilot;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 2 · Herramientas a medida con AIFunctionFactory.
/// get_stock_price skips permission; place_trade goes through OnPermissionRequest and asks the user.
/// </summary>
public static class Step02_CustomTools
{
    public static T Configure<T>(T config) where T : SessionConfigBase
    {
        Step01_Hello.Configure(config);

        config.Tools = FinanceTools.All();
        config.OnPermissionRequest = Approvals.TradesOnly;
        return config;
    }

    public static async Task RunAsync()
    {
        await using var client = await ClawClient.StartAsync();
        await using var session = await client.CreateSessionAsync(Configure(new SessionConfig()));

        await ConsoleChat.RunAsync(session, "Paso 2 · Herramientas a medida");
    }
}
