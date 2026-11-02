using System.ComponentModel;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace FinanceClaw.Tools;

/// <summary>
/// Custom tools built with AIFunctionFactory (Microsoft.Extensions.AI).
/// The Copilot runtime calls back into this process when the model invokes them.
/// </summary>
public static class FinanceTools
{
    public const string GetStockPriceName = "get_stock_price";
    public const string PlaceTradeName = "place_trade";

    [Description("Gets the latest price for a stock ticker symbol (e.g. MSFT, AAPL).")]
    public static StockQuote GetStockPrice(
        [Description("Ticker symbol, e.g. MSFT")] string symbol) => MarketData.GetQuote(symbol);

    [Description("Places a simulated buy or sell order. Always requires the user's approval.")]
    public static string PlaceTrade(
        [Description("Ticker symbol, e.g. MSFT")] string symbol,
        [Description("Either 'buy' or 'sell'")] string action,
        [Description("Number of shares, greater than zero")] int quantity)
    {
        if (action is not ("buy" or "sell"))
            return "Rejected: action must be 'buy' or 'sell'.";
        if (quantity <= 0)
            return "Rejected: quantity must be greater than zero.";

        var quote = MarketData.GetQuote(symbol);
        var id = $"TRADE-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        return $"{id}: {action} {quantity} {quote.Symbol} @ {quote.Price} {quote.Currency} " +
               $"(total {quote.Price * quantity:0.00}). Simulated, no real order was sent.";
    }

    /// <summary>Read-only quote lookup: skip the permission prompt.</summary>
    public static AIFunction CreateGetStockPriceTool() =>
        CopilotTool.DefineTool(
            GetStockPrice,
            new CopilotToolOptions { SkipPermission = true },
            new AIFunctionFactoryOptions { Name = GetStockPriceName });

    /// <summary>
    /// Plain AIFunctionFactory tool. Without SkipPermission, every call goes through OnPermissionRequest
    /// as a PermissionRequestCustomTool, which is where we ask the user.
    /// </summary>
    public static AIFunction CreatePlaceTradeTool() =>
        AIFunctionFactory.Create(PlaceTrade, PlaceTradeName);

    public static List<AIFunctionDeclaration> All() => [CreateGetStockPriceTool(), CreatePlaceTradeTool()];
}
