using System.Globalization;
using FinanceClaw.Shared;
using FinanceClaw.Tools;
using FinanceClaw.Web;
using GitHub.Copilot;

LoadDotEnv(Path.Combine(Paths.Root, ".env"));

var builder = WebApplication.CreateBuilder(args);

// Whoever can reach this server can approve trades and shell commands, so it stays on this machine.
builder.WebHost.UseUrls("http://localhost:5199");
builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1";
builder.Services.AddSingleton<ClawHost>();

var app = builder.Build();

// Blocks other sites open in the same browser from driving the agent.
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();
    if (origin.Length > 0 && !(Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    await next();
});

app.UseDefaultFiles();
// Revalidate on every load (a cheap 304 when nothing changed), so a browser never mixes new HTML with an old app.js.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});

app.MapGet("/api/steps", (string? lang) => StepCatalog.For(lang).Select(StepCatalog.ToClient));

app.MapGet("/api/portfolio", () =>
{
    var path = Path.Combine(Paths.Workspace, "portfolio.csv");
    var positions = File.ReadAllLines(path)
        .Skip(1)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => line.Split(','))
        .Select(cols =>
        {
            var shares = decimal.Parse(cols[1], CultureInfo.InvariantCulture);
            var costBasis = decimal.Parse(cols[2], CultureInfo.InvariantCulture);
            var price = MarketData.GetQuote(cols[0]).Price;
            return new { symbol = cols[0], shares, price, value = shares * price, cost = shares * costBasis };
        })
        .ToList();

    return new { positions, totalValue = positions.Sum(p => p.value), totalCost = positions.Sum(p => p.cost) };
});

app.MapGet("/api/sessions/saved", async (ClawHost host) =>
{
    var client = await host.GetClientAsync();
    var sessions = await client.ListSessionsAsync();
    return sessions
        .Where(s => s.SessionId.StartsWith("claw-", StringComparison.Ordinal))
        .OrderByDescending(s => s.ModifiedTime)
        .Take(5)
        .Select(s => new { id = s.SessionId, modified = s.ModifiedTime, summary = s.Summary });
});

app.MapPost("/api/runs", async (StartRequest request, ClawHost host) =>
{
    if (StepCatalog.All.All(s => s.Number != request.Step))
        return Results.BadRequest(new { error = "Paso desconocido." });

    try
    {
        var run = await host.StartAsync(request.Step, request.ResumeSessionId);
        return Results.Ok(new { id = run.Id, step = run.Step, sessionId = run.SessionId, workspace = run.WorkspacePath });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/runs/{id}/events", async (string id, ClawHost host, HttpContext context, CancellationToken cancellationToken) =>
{
    if (host.Find(id) is not { } run)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";

    await foreach (var json in run.ReadEventsAsync(cancellationToken))
    {
        await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }
});

app.MapPost("/api/runs/{id}/messages", (string id, MessageRequest request, ClawHost host) =>
    host.Find(id) is not { } run ? Results.NotFound()
    : string.IsNullOrWhiteSpace(request.Prompt) ? Results.BadRequest()
    : run.TrySend(request.Prompt) ? Results.Accepted()
    : Results.Conflict(new { error = "El agente sigue trabajando." }));

app.MapPost("/api/runs/{id}/stop", async (string id, ClawHost host) =>
{
    if (host.Find(id) is not { } run)
        return Results.NotFound();

    await run.StopTurnAsync();
    return Results.Accepted();
});

app.MapPost("/api/runs/{id}/approvals/{approvalId}", (string id, string approvalId, ApprovalRequest request, ClawHost host) =>
    host.Find(id)?.ResolveApproval(approvalId, request.Approved) == true ? Results.Ok() : Results.NotFound());

app.Run();

// .env holds the Foundry settings for step 7. Real environment variables win.
static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
        return;

    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line[0] == '#')
            continue;

        if (line.StartsWith("export ", StringComparison.Ordinal))
            line = line["export ".Length..];

        var separator = line.IndexOf('=');
        if (separator <= 0)
            continue;

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim().Trim('"', '\'');
        if (!value.Contains('<') && Environment.GetEnvironmentVariable(key) is null)
            Environment.SetEnvironmentVariable(key, value);
    }
}

internal sealed record StartRequest(int Step, string? ResumeSessionId);
internal sealed record MessageRequest(string Prompt);
internal sealed record ApprovalRequest(bool Approved);

namespace FinanceClaw.Web
{
    /// <summary>One Copilot runtime for the whole app and one active run: this is a single-presenter demo.</summary>
    public sealed class ClawHost : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Lazy<Task<CopilotClient>> _client = new(ClawClient.StartAsync);
        private ClawRun? _current;

        public Task<CopilotClient> GetClientAsync() => _client.Value;

        public ClawRun? Find(string id) => _current?.Id == id ? _current : null;

        public async Task<ClawRun> StartAsync(int step, string? resumeSessionId)
        {
            await _gate.WaitAsync();
            try
            {
                if (_current is not null)
                {
                    await _current.DisposeAsync();
                    _current = null;
                }

                _current = await ClawRun.StartAsync(await GetClientAsync(), step, resumeSessionId);
                return _current;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_current is not null)
                await _current.DisposeAsync();
            if (_client.IsValueCreated)
                await (await _client.Value).DisposeAsync();
        }
    }
}
