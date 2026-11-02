using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Stdio MCP server with mock market data. Copilot launches it as a child process (see Step05_Mcp).
var builder = Host.CreateApplicationBuilder(args);

// stdout is the MCP channel: logs must go to stderr.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MarketTools>();

await builder.Build().RunAsync();

[McpServerToolType]
public sealed class MarketTools
{
    private static readonly Dictionary<string, (string Name, string Sector, string Summary)> Companies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MSFT"] = ("Microsoft", "Technology", "Cloud (Azure), productivity software and AI platforms."),
            ["AAPL"] = ("Apple", "Technology", "Consumer hardware, services and wearables."),
            ["NVDA"] = ("NVIDIA", "Semiconductors", "GPUs and accelerated computing for AI and gaming."),
            ["AMZN"] = ("Amazon", "Consumer / Cloud", "E-commerce, logistics and AWS."),
            ["GOOGL"] = ("Alphabet", "Communication Services", "Search, advertising, YouTube and Google Cloud."),
            ["SPY"] = ("SPDR S&P 500 ETF", "ETF", "Tracks the S&P 500 index."),
        };

    private static readonly Dictionary<string, decimal> UsdRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1m, ["EUR"] = 0.86m, ["GBP"] = 0.75m, ["JPY"] = 152.4m, ["MXN"] = 18.5m,
    };

    [McpServerTool(Name = "get_company_profile", ReadOnly = true, Idempotent = true)]
    [Description("Company name, sector and a one-line business summary for a ticker.")]
    public static object GetCompanyProfile([Description("Ticker symbol")] string symbol) =>
        Companies.TryGetValue(symbol, out var c)
            ? new { symbol = symbol.ToUpperInvariant(), c.Name, c.Sector, c.Summary }
            : new { symbol = symbol.ToUpperInvariant(), Name = "Unknown", Sector = "Unknown", Summary = "No profile available." };

    [McpServerTool(Name = "get_market_news", ReadOnly = true)]
    [Description("Latest (mock) headlines for a ticker, newest first.")]
    public static object[] GetMarketNews(
        [Description("Ticker symbol")] string symbol,
        [Description("Max headlines, 1-5")] int count = 3)
    {
        symbol = symbol.ToUpperInvariant();
        var name = Companies.TryGetValue(symbol, out var c) ? c.Name : symbol;
        string[] headlines =
        [
            $"{name} beats quarterly revenue estimates on strong demand",
            $"Analysts raise price target for {name} after investor day",
            $"{name} faces regulatory questions in the EU",
            $"{name} announces expanded share buyback program",
            $"Supply chain concerns weigh on {name} outlook",
        ];

        return headlines
            .Take(Math.Clamp(count, 1, 5))
            .Select((h, i) => (object)new { symbol, headline = h, publishedAt = DateTime.UtcNow.AddHours(-6 * (i + 1)) })
            .ToArray();
    }

    [McpServerTool(Name = "convert_currency", ReadOnly = true, Idempotent = true)]
    [Description("Converts an amount between USD, EUR, GBP, JPY and MXN using mock rates.")]
    public static object ConvertCurrency(decimal amount, string from, string to)
    {
        if (!UsdRates.TryGetValue(from, out var fromRate) || !UsdRates.TryGetValue(to, out var toRate))
            return new { error = "Supported currencies: " + string.Join(", ", UsdRates.Keys) };

        return new { amount, from, to, result = Math.Round(amount / fromRate * toRate, 2), rate = toRate / fromRate };
    }
}
