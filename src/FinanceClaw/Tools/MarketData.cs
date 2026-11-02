namespace FinanceClaw.Tools;

public sealed record StockQuote(string Symbol, decimal Price, string Currency, DateTimeOffset AsOf);

/// <summary>
/// Mock market data, same spirit as the "Build your own claw" sample: no real feed, deterministic prices.
/// </summary>
public static class MarketData
{
    private static readonly Dictionary<string, decimal> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSFT"] = 512.30m,
        ["AAPL"] = 231.85m,
        ["GOOGL"] = 198.40m,
        ["AMZN"] = 224.10m,
        ["NVDA"] = 178.60m,
        ["SPY"] = 641.20m,
    };

    public static StockQuote GetQuote(string symbol)
    {
        symbol = symbol.Trim().ToUpperInvariant();

        // Unknown tickers get a stable made-up price so the demo never fails.
        var price = Prices.TryGetValue(symbol, out var known)
            ? known
            : 50m + Math.Abs(symbol.GetHashCode() % 20_000) / 100m;

        return new StockQuote(symbol, price, "USD", DateTimeOffset.UtcNow);
    }
}
