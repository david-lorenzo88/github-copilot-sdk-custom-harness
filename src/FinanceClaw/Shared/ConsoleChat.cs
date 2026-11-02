using GitHub.Copilot;

namespace FinanceClaw.Shared;

/// <summary>Minimal REPL that streams the agent's output and shows what the harness is doing.</summary>
public static class ConsoleChat
{
    public static async Task RunAsync(CopilotSession session, string title)
    {
        Ui.Header(title);
        Ui.Dim($"session: {session.SessionId}  ·  escribe 'exit' para salir");

        using var _ = session.On<SessionEvent>(Render);

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\n> ");
            Console.ResetColor();

            var prompt = Console.ReadLine();
            if (prompt is null || prompt.Trim() is "exit" or "quit")
                break;
            if (string.IsNullOrWhiteSpace(prompt))
                continue;

            try
            {
                await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt }, TimeSpan.FromMinutes(10));
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Ui.Error(ex.Message);
            }
        }
    }

    private static void Render(SessionEvent evt)
    {
        switch (evt)
        {
            // Only the root agent's text; sub-agents report back through their tool call.
            case AssistantMessageDeltaEvent { AgentId: null } delta:
                Console.Write(delta.Data.DeltaContent);
                break;
            case ToolExecutionStartEvent tool:
                Ui.Dim($"\n  ⚙ {tool.Data.ToolName} {Ui.Truncate(tool.Data.Arguments?.GetRawText(), 120)}");
                break;
            case ToolExecutionCompleteEvent { Data.Success: false } failed:
                Ui.Dim($"  ✗ {failed.Data.Error?.Message}");
                break;
            case SkillInvokedEvent skill:
                Ui.Info($"\n  ★ skill: {skill.Data.Name}");
                break;
            case SubagentStartedEvent sub:
                Ui.Info($"\n  ↳ sub-agent: {sub.Data.AgentDisplayName ?? sub.Data.AgentName}");
                break;
            case SessionCompactionStartEvent:
                Ui.Info("\n  ⟳ compactando contexto…");
                break;
            case SessionCompactionCompleteEvent done:
                Ui.Info($"  ⟳ compactado: {done.Data.PreCompactionTokens} → {done.Data.PostCompactionTokens} tokens");
                break;
            case SessionErrorEvent error:
                Ui.Error(error.Data.Message);
                break;
        }
    }
}

public static class Ui
{
    public static void Header(string text) => Write(ConsoleColor.Cyan, $"\n=== {text} ===");
    public static void Info(string text) => Write(ConsoleColor.Yellow, text);
    public static void Dim(string text) => Write(ConsoleColor.DarkGray, text);
    public static void Error(string text) => Write(ConsoleColor.Red, $"\n  ! {text}");

    public static bool Confirm(string question)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"\n  ? {question} [s/N] ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim().ToLowerInvariant() is "s" or "si" or "sí" or "y" or "yes";
    }

    public static string Truncate(string? text, int max) =>
        text is null ? "" : text.Length <= max ? text : text[..max] + "…";

    private static void Write(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
