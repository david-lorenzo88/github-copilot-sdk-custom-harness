using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace FinanceClaw.Shared;

/// <summary>
/// OnPermissionRequest handlers. The runtime asks before every tool execution;
/// we decide by request kind (custom tool, read, write, shell, MCP, URL…).
/// </summary>
public static class Approvals
{
    /// <summary>Step 1: nothing runs yet.</summary>
    public static Task<PermissionDecision> DenyAll(PermissionRequest request, PermissionInvocation _) =>
        Task.FromResult<PermissionDecision>(PermissionDecision.Reject("Tools are not enabled in this step."));

    /// <summary>Step 2: only our custom tools, and trades need a human.</summary>
    public static Task<PermissionDecision> TradesOnly(PermissionRequest request, PermissionInvocation _) =>
        Task.FromResult<PermissionDecision>(request switch
        {
            PermissionRequestCustomTool tool => AskForTrade(tool),
            _ => PermissionDecision.Reject("Only the finance tools are enabled in this step."),
        });

    /// <summary>Step 3+: built-in file and shell tools, scoped to the workspace.</summary>
    public static Task<PermissionDecision> Portfolio(PermissionRequest request, PermissionInvocation _) =>
        Task.FromResult<PermissionDecision>(request switch
        {
            PermissionRequestCustomTool tool => AskForTrade(tool),

            PermissionRequestRead read when IsInside(read.ResolvedPath ?? read.Path, Paths.Root)
                => PermissionDecision.ApproveOnce(),

            PermissionRequestWrite write when IsInside(write.ResolvedPath ?? write.FileName, Paths.Workspace)
                => Ui.Confirm($"¿Escribir {Relative(write.ResolvedPath ?? write.FileName)}?")
                    ? PermissionDecision.ApproveOnce()
                    : PermissionDecision.Reject("The user declined the file change."),

            PermissionRequestShell shell
                => Ui.Confirm($"¿Ejecutar `{shell.FullCommandText}`?")
                    ? PermissionDecision.ApproveOnce()
                    : PermissionDecision.Reject("The user declined the command."),

            // Read-only MCP tools (annotated by the server) run without asking.
            PermissionRequestMcp { ReadOnly: true } => PermissionDecision.ApproveOnce(),
            PermissionRequestMcp mcp
                => Ui.Confirm($"¿Llamar a {mcp.ServerName}/{mcp.ToolName}?")
                    ? PermissionDecision.ApproveOnce()
                    : PermissionDecision.Reject("The user declined the MCP call."),

            PermissionRequestRead or PermissionRequestWrite
                => PermissionDecision.Reject("Only files inside the claw workspace are allowed."),

            _ => PermissionDecision.Reject($"'{request.Kind}' is not allowed for Finance Claw."),
        });

    private static PermissionDecision AskForTrade(PermissionRequestCustomTool tool)
    {
        if (tool.ToolName != Tools.FinanceTools.PlaceTradeName)
            return PermissionDecision.ApproveOnce();

        Ui.Info($"\n  💸 {tool.ToolName} {tool.Args?.GetRawText()}");
        return Ui.Confirm("¿Aprobar la operación?")
            ? PermissionDecision.ApproveOnce()
            : PermissionDecision.Reject("The user rejected the trade.");
    }

    public static bool IsInside(string? path, string folder) =>
        path is not null &&
        Path.GetFullPath(path, Paths.Workspace).StartsWith(Path.GetFullPath(folder), StringComparison.Ordinal);

    private static string Relative(string? path) =>
        path is null ? "?" : Path.GetRelativePath(Paths.Workspace, Path.GetFullPath(path, Paths.Workspace));
}
