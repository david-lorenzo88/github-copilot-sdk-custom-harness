using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using FinanceClaw.Shared;
using GitHub.Copilot;
using Microsoft.Agents.AI;

namespace FinanceClaw.Web;

/// <summary>
/// One live agent for one step. Turns SDK events into JSON for the browser and
/// parks permission requests until the presenter clicks approve or reject.
/// </summary>
public sealed class ClawRun : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ApprovalTimeout = TimeSpan.FromMinutes(5);

    private readonly Channel<string> _events = Channel.CreateUnbounded<string>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _approvals = new();
    private readonly CancellationTokenSource _closed = new();

    private CopilotSession? _session;
    private AIAgent? _agent;
    private AgentSession? _agentSession;
    private CancellationTokenSource? _turn;
    private int _busy;

    private ClawRun(int step) => Step = step;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public int Step { get; }
    public string SessionId => _session?.SessionId ?? "agent-framework";
    public string? WorkspacePath => _session?.WorkspacePath;

    public static async Task<ClawRun> StartAsync(CopilotClient client, int step, string? resumeSessionId)
    {
        var run = new ClawRun(step);

        if (step == StepCatalog.AgentFrameworkStep)
        {
            run._agent = client.AsAIAgent(run.Prepare(new SessionConfig()), name: "FinanceClaw");
            run._agentSession = await run._agent.CreateSessionAsync();
        }
        else if (resumeSessionId is not null)
        {
            run._session = await client.ResumeSessionAsync(resumeSessionId, run.Prepare(new ResumeSessionConfig()));
            await run.ReplayHistoryAsync();
        }
        else
        {
            var config = run.Prepare(new SessionConfig());
            if (step == StepCatalog.SessionsStep)
                config.SessionId = $"claw-{DateTime.Now:yyyyMMdd-HHmmss}";

            run._session = await client.CreateSessionAsync(config);
        }

        return run;
    }

    public IAsyncEnumerable<string> ReadEventsAsync(CancellationToken cancellationToken) =>
        _events.Reader.ReadAllAsync(cancellationToken);

    public bool TrySend(string prompt)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return false;

        _turn = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
        var cancellationToken = _turn.Token;
        Emit(new { type = "turn-start", prompt });

        _ = Task.Run(async () =>
        {
            try
            {
                if (_agent is not null)
                {
                    await foreach (var update in _agent.RunStreamingAsync(prompt, _agentSession, cancellationToken: cancellationToken))
                    {
                        if (!string.IsNullOrEmpty(update.Text))
                            Emit(new { type = "delta", text = update.Text });
                    }
                }
                else
                {
                    await _session!.SendAndWaitAsync(new MessageOptions { Prompt = prompt }, TimeSpan.FromMinutes(10), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped by the presenter or the run was closed.
            }
            catch (Exception ex)
            {
                Emit(new { type = "error", message = ex.Message });
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
                Emit(new { type = "turn-end" });
            }
        }, CancellationToken.None);

        return true;
    }

    public async Task StopTurnAsync()
    {
        if (_session is not null)
            await _session.AbortAsync();

        _turn?.Cancel();
    }

    public bool ResolveApproval(string approvalId, bool approved) =>
        _approvals.TryGetValue(approvalId, out var pending) && pending.TrySetResult(approved);

    public async ValueTask DisposeAsync()
    {
        await _closed.CancelAsync();
        _events.Writer.TryComplete();

        if (_session is not null)
            await _session.DisposeAsync();
        if (_agent is IAsyncDisposable agent)
            await agent.DisposeAsync();
    }

    /// <summary>Same step configuration as the console, with browser approvals and a live event feed on top.</summary>
    private T Prepare<T>(T config) where T : SessionConfigBase
    {
        StepCatalog.Configure(Step, config);

        var policy = new PermissionPolicy(ConfirmInBrowserAsync);
        config.OnPermissionRequest = Step switch
        {
            1 => policy.DenyAll,
            2 => policy.TradesOnly,
            _ => policy.Portfolio,
        };
        config.OnEvent = Render;

        if (config.Hooks is { } hooks)
            ObserveHooks(hooks);

        return config;
    }

    /// <summary>Wraps the hooks from AuditHooks so their decisions also reach the browser.</summary>
    private void ObserveHooks(SessionHooks hooks)
    {
        if (hooks.OnPreToolUse is { } preToolUse)
        {
            hooks.OnPreToolUse = async (input, invocation) =>
            {
                var output = await preToolUse(input, invocation);
                if (output?.PermissionDecision == "deny")
                    Emit(new { type = "hook-blocked", target = input.ToolName, reason = output.PermissionDecisionReason });
                return output;
            };
        }

        if (hooks.OnUserPromptSubmitted is { } promptSubmitted)
        {
            hooks.OnUserPromptSubmitted = async (input, invocation) =>
            {
                var output = await promptSubmitted(input, invocation);
                if (output?.Decision == "block")
                    Emit(new { type = "hook-blocked", target = "prompt", reason = output.Reason });
                return output;
            };
        }

        if (hooks.OnPostToolUse is { } postToolUse)
        {
            hooks.OnPostToolUse = async (input, invocation) =>
            {
                Emit(new
                {
                    type = "audit",
                    at = input.Timestamp,
                    agent = input.AgentType ?? "main",
                    tool = input.ToolName,
                    args = Ui.Truncate(input.ToolArgs?.GetRawText(), 300),
                });
                return await postToolUse(input, invocation);
            };
        }
    }

    private async Task<bool> ConfirmInBrowserAsync(ApprovalPrompt prompt)
    {
        var id = Guid.NewGuid().ToString("N");
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _approvals[id] = pending;

        Emit(new
        {
            type = "approval-request",
            id,
            kind = prompt.Kind,
            question = prompt.Question,
            detail = prompt.Detail,
            timeoutSeconds = (int)ApprovalTimeout.TotalSeconds,
        });

        // No answer counts as a rejection: nothing runs without a human saying yes.
        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
        expiry.CancelAfter(ApprovalTimeout);
        await using var registration = expiry.Token.Register(() => pending.TrySetResult(false));

        var approved = await pending.Task;
        _approvals.TryRemove(id, out _);
        Emit(new { type = "approval-resolved", id, approved });
        return approved;
    }

    private async Task ReplayHistoryAsync()
    {
        foreach (var evt in await _session!.GetEventsAsync())
        {
            switch (evt)
            {
                case UserMessageEvent user:
                    Emit(new { type = "history", role = "user", text = user.Data.Content });
                    break;
                case AssistantMessageEvent { AgentId: null } assistant when !string.IsNullOrWhiteSpace(assistant.Data.Content):
                    Emit(new { type = "history", role = "assistant", text = assistant.Data.Content });
                    break;
            }
        }
    }

    private void Render(SessionEvent evt)
    {
        var nested = evt.AgentId is not null;
        switch (evt)
        {
            // Agent Framework streams the text itself; only the root agent's text goes to the chat.
            case AssistantMessageDeltaEvent { AgentId: null } delta when _agent is null:
                Emit(new { type = "delta", text = delta.Data.DeltaContent });
                break;
            case ToolExecutionStartEvent tool:
                Emit(new
                {
                    type = "tool-start",
                    id = tool.Data.ToolCallId,
                    name = tool.Data.ToolName,
                    mcpServer = tool.Data.McpServerName,
                    args = Ui.Truncate(tool.Data.Arguments?.GetRawText(), 600),
                    nested,
                });
                break;
            case ToolExecutionCompleteEvent done:
                Emit(new { type = "tool-end", id = done.Data.ToolCallId, success = done.Data.Success, error = done.Data.Error?.Message });
                break;
            case SkillInvokedEvent skill:
                Emit(new { type = "skill", name = skill.Data.Name, description = skill.Data.Description });
                break;
            case SubagentStartedEvent sub:
                Emit(new { type = "subagent-start", id = sub.Data.ToolCallId, name = sub.Data.AgentDisplayName ?? sub.Data.AgentName });
                break;
            case SubagentCompletedEvent sub:
                Emit(new { type = "subagent-end", id = sub.Data.ToolCallId, name = sub.Data.AgentDisplayName ?? sub.Data.AgentName, tools = sub.Data.TotalToolCalls, tokens = sub.Data.TotalTokens });
                break;
            case SessionCompactionStartEvent:
                Emit(new { type = "compaction-start" });
                break;
            case SessionCompactionCompleteEvent compaction:
                Emit(new { type = "compaction-end", before = compaction.Data.PreCompactionTokens, after = compaction.Data.PostCompactionTokens });
                break;
            case SessionUsageInfoEvent usage:
                Emit(new { type = "context", current = usage.Data.CurrentTokens, limit = usage.Data.TokenLimit });
                break;
            case AssistantUsageEvent usage when !nested:
                Emit(new { type = "usage", model = usage.Data.ModelDisplayName ?? usage.Data.Model, input = usage.Data.InputTokens, output = usage.Data.OutputTokens, cached = usage.Data.CacheReadTokens });
                break;
            // Blocked prompts are already reported as hook-blocked.
            case SessionWarningEvent warning when warning.Data.WarningType != "policy_blocked":
                Emit(new { type = "warning", message = warning.Data.Message });
                break;
            case SessionErrorEvent error:
                Emit(new { type = "error", message = error.Data.Message });
                break;
        }
    }

    private void Emit(object payload) =>
        _events.Writer.TryWrite(JsonSerializer.Serialize(payload, Json));
}
