"""Valuation metrics for a portfolio CSV. Usage: valuation_metrics.py portfolio.csv SYMBOL=PRICE ..."""
import csv
import json
import sys
from datetime import date


def main() -> None:
    path, *pairs = sys.argv[1:]
    prices = {k.upper(): float(v) for k, v in (p.split("=", 1) for p in pairs)}

    rows = []
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            symbol = r["symbol"].upper()
            if symbol not in prices:
                print(json.dumps({"error": f"missing price for {symbol}"}))
                sys.exit(1)
            shares, cost_basis = float(r["shares"]), float(r["cost_basis"])
            value, cost = shares * prices[symbol], shares * cost_basis
            rows.append({
                "symbol": symbol,
                "shares": shares,
                "price": prices[symbol],
                "market_value": round(value, 2),
                "cost": round(cost, 2),
                "pnl": round(value - cost, 2),
                "pnl_pct": round((value - cost) / cost * 100, 2),
                "holding_days": (date.today() - date.fromisoformat(r["purchase_date"])).days,
            })

    total = sum(r["market_value"] for r in rows)
    for r in rows:
        r["weight_pct"] = round(r["market_value"] / total * 100, 2)

    total_cost = sum(r["cost"] for r in rows)
    print(json.dumps({
        "positions": rows,
        "total_value": round(total, 2),
        "total_cost": round(total_cost, 2),
        "total_pnl": round(total - total_cost, 2),
        "total_pnl_pct": round((total - total_cost) / total_cost * 100, 2),
    }, indent=2))


if __name__ == "__main__":
    main()
