---
name: risk-scoring
description: Scores portfolio concentration and sector risk from 1 (low) to 10 (high). Use when asked about risk, diversification or rebalancing.
---

# Risk scoring

1. Get position weights first (use the `valuation` skill if you don't have them).
2. Map each symbol to a sector. Use `market-get_company_profile` when it is available; otherwise use `references/sectors.md`.
3. Run the scoring script with `SYMBOL=WEIGHT_PCT:SECTOR` triples:

   ```bash
   python3 <this skill folder>/scripts/risk_score.py MSFT=24.1:Technology NVDA=12.3:Semiconductors ...
   ```

4. Explain the score in 3 bullets: single-name concentration, sector concentration, number of positions.
5. Suggest at most two rebalancing ideas. Do not place trades unless the user asks; trades go through `place_trade`.
6. End with the not-a-financial-advisor disclaimer.
