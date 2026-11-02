using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot;

namespace FinanceClaw.Shared;

/// <summary>
/// Session hooks: they see every tool call and can allow, deny, rewrite or audit it.
/// Permissions answer "may this run?"; hooks add policy and side effects around it.
/// </summary>
public static class AuditHooks
{
    // Built-in tools that only read. Anything else touching portfolio.csv is a write.
    private static readonly HashSet<string> ReadOnlyTools = ["view", "glob", "grep", "rg", "read_file", "list_dir"];

    public static SessionHooks Create() => new()
    {
        OnSessionStart = (input, _) => Task.FromResult<SessionStartHookOutput?>(new()
        {
            AdditionalContext = $"Today is {DateTime.Now:yyyy-MM-dd}. Session source: {input.Source}.",
        }),

        OnUserPromptSubmitted = (input, _) => Task.FromResult<UserPromptSubmittedHookOutput?>(
            input.Prompt.Contains("insider", StringComparison.OrdinalIgnoreCase) ||
            input.Prompt.Contains("información privilegiada", StringComparison.OrdinalIgnoreCase)
                ? new() { Decision = "block", Reason = "Finance Claw no opera con información privilegiada." }
                : null),

        OnPreToolUse = (input, _) =>
        {
            var args = input.ToolArgs?.GetRawText() ?? "";

            // The portfolio only changes through place_trade, never by editing the file.
            var touchesPortfolio = args.Contains("portfolio.csv", StringComparison.OrdinalIgnoreCase);
            var writes = input.ToolName == "bash" ? IsShellWrite(args) : !ReadOnlyTools.Contains(input.ToolName);
            if (touchesPortfolio && writes)
            {
                Ui.Info($"\n  🛡 hook: bloqueado {input.ToolName} sobre portfolio.csv");
                return Task.FromResult<PreToolUseHookOutput?>(new()
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = "portfolio.csv is read-only. Use place_trade to change holdings.",
                });
            }

            return Task.FromResult<PreToolUseHookOutput?>(null);
        },

        OnPostToolUse = async (input, _) =>
        {
            var entry = JsonSerializer.Serialize(new
            {
                at = input.Timestamp,
                agent = input.AgentType ?? "main",
                tool = input.ToolName,
                args = input.ToolArgs,
            });
            await File.AppendAllTextAsync(Paths.AuditLog, entry + Environment.NewLine);

            // Side effect owned by the app, not the model: persist trade confirmations.
            if (input.ToolName == Tools.FinanceTools.PlaceTradeName &&
                Regex.Match(input.ToolResult?.GetRawText() ?? "", @"TRADE-[A-Z0-9]{8}") is { Success: true } id)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(Paths.Workspace, "confirmations", $"{id.Value}.txt"),
                    $"{id.Value}\n{input.ToolArgs?.GetRawText()}\n{input.Timestamp:O}\n");
            }

            return null;
        },
    };

    private static bool IsShellWrite(string args) =>
        args.Contains('>') || args.Contains("sed -i") || args.Contains("rm ") || args.Contains("mv ");
}
