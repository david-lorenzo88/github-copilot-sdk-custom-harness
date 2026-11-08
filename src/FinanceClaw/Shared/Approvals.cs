using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace FinanceClaw.Shared;

/// <summary>What a human is asked to approve. Kind is one of trade, write, shell or mcp.</summary>
public sealed record ApprovalPrompt(string Kind, string Question, string? Detail = null);

/// <summary>
/// OnPermissionRequest handlers. The runtime asks before every tool execution;
/// we decide by request kind (custom tool, read, write, shell, MCP, URL…).
/// How a human is asked is up to the host: the console or a browser dialog.
/// </summary>
public sealed class PermissionPolicy(Func<ApprovalPrompt, Task<bool>> confirm)
{
    /// <summary>Step 1: nothing runs yet.</summary>
    public Task<PermissionDecision> DenyAll(PermissionRequest request, PermissionInvocation _) =>
        Task.FromResult<PermissionDecision>(PermissionDecision.Reject("Tools are not enabled in this step."));

    /// <summary>Step 2: only our custom tools, and trades need a human.</summary>
    public async Task<PermissionDecision> TradesOnly(PermissionRequest request, PermissionInvocation _) =>
        request switch
        {
            PermissionRequestCustomTool tool => await AskForTrade(tool),
            _ => PermissionDecision.Reject("Only the finance tools are enabled in this step."),
        };

    /// <summary>Step 3+: built-in file and shell tools, scoped to the workspace.</summary>
    public async Task<PermissionDecision> Portfolio(PermissionRequest request, PermissionInvocation _) =>
        request switch
        {
            PermissionRequestCustomTool tool => await AskForTrade(tool),

            PermissionRequestRead read when IsInside(read.ResolvedPath ?? read.Path, Paths.Root)
                => PermissionDecision.ApproveOnce(),

            PermissionRequestWrite write when IsInside(write.ResolvedPath ?? write.FileName, Paths.Workspace)
                => await Ask(
                    new("write", $"¿Escribir {Relative(write.ResolvedPath ?? write.FileName)}?", write.Diff ?? write.NewFileContents),
                    "The user declined the file change."),

            PermissionRequestShell shell when IsTrustedCommand(shell.FullCommandText)
                => PermissionDecision.ApproveOnce(),
            PermissionRequestShell shell
                => await Ask(new("shell", "¿Ejecutar este comando?", shell.FullCommandText), "The user declined the command."),

            // Read-only MCP tools (annotated by the server) run without asking.
            PermissionRequestMcp { ReadOnly: true } => PermissionDecision.ApproveOnce(),
            PermissionRequestMcp mcp
                => await Ask(new("mcp", $"¿Llamar a {mcp.ServerName}/{mcp.ToolName}?", mcp.Args?.GetRawText()), "The user declined the MCP call."),

            PermissionRequestRead or PermissionRequestWrite
                => PermissionDecision.Reject("Only files inside the claw workspace are allowed."),

            _ => PermissionDecision.Reject($"'{request.Kind}' is not allowed for Finance Claw."),
        };

    private async Task<PermissionDecision> AskForTrade(PermissionRequestCustomTool tool)
    {
        if (tool.ToolName != Tools.FinanceTools.PlaceTradeName)
            return PermissionDecision.ApproveOnce();

        return await Ask(new("trade", "¿Aprobar la operación?", tool.Args?.GetRawText()), "The user rejected the trade.");
    }

    private async Task<PermissionDecision> Ask(ApprovalPrompt prompt, string rejection) =>
        await confirm(prompt) ? PermissionDecision.ApproveOnce() : PermissionDecision.Reject(rejection);

    /// <summary>Skill scripts we ship, and plain read-only commands with no chaining or redirection.</summary>
    private static bool IsTrustedCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.IndexOfAny([';', '&', '|', '>', '<', '`', '$']) >= 0)
            return false;

        return command.StartsWith($"python3 {Paths.Skills}{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               || command.Split(' ')[0] is "cat" or "head" or "tail" or "ls" or "wc";
    }

    private static bool IsInside(string? path, string folder) =>
        path is not null &&
        Path.GetFullPath(path, Paths.Workspace).StartsWith(Path.GetFullPath(folder), StringComparison.Ordinal);

    private static string Relative(string? path) =>
        path is null ? "?" : Path.GetRelativePath(Paths.Workspace, Path.GetFullPath(path, Paths.Workspace));
}

/// <summary>The console host: approvals are s/N prompts in the terminal.</summary>
public static class Approvals
{
    private static readonly PermissionPolicy ConsolePolicy = new(prompt =>
    {
        if (prompt.Kind is "trade" or "shell")
            Ui.Info($"\n  {prompt.Detail}");

        return Task.FromResult(Ui.Confirm(prompt.Question));
    });

    public static Task<PermissionDecision> DenyAll(PermissionRequest request, PermissionInvocation invocation) =>
        ConsolePolicy.DenyAll(request, invocation);

    public static Task<PermissionDecision> TradesOnly(PermissionRequest request, PermissionInvocation invocation) =>
        ConsolePolicy.TradesOnly(request, invocation);

    public static Task<PermissionDecision> Portfolio(PermissionRequest request, PermissionInvocation invocation) =>
        ConsolePolicy.Portfolio(request, invocation);
}
