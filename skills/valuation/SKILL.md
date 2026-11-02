---
name: valuation
description: Values the user's portfolio. Use when asked how much the portfolio is worth, gains/losses, or position weights.
---

# Portfolio valuation

1. Read `portfolio.csv` from the working directory (columns: symbol, shares, cost_basis, purchase_date).
2. Get the current price of every symbol with `get_stock_price`. Never guess a price.
3. Run the metrics script with the prices you got, as `SYMBOL=PRICE` pairs:

   ```bash
   python3 <this skill folder>/scripts/valuation_metrics.py portfolio.csv MSFT=512.30 AAPL=231.85 ...
   ```

4. Present a Markdown table: symbol, shares, price, market value, unrealized P/L (amount and %), weight.
5. Finish with total value, total P/L and the largest position. Use the definitions in `references/metrics.md`.
6. If the user asks for a report, write it to `reports/valuation-<yyyy-MM-dd>.md`.
