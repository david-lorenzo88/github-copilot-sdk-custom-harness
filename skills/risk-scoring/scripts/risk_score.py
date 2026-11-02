"""Concentration risk score. Usage: risk_score.py SYMBOL=WEIGHT_PCT:SECTOR ..."""
import json
import sys
from collections import defaultdict


def main() -> None:
    positions = []
    for arg in sys.argv[1:]:
        symbol, rest = arg.split("=", 1)
        weight, sector = rest.split(":", 1)
        positions.append((symbol.upper(), float(weight), sector))

    sectors = defaultdict(float)
    for _, weight, sector in positions:
        sectors[sector] += weight

    top_name = max(positions, key=lambda p: p[1])
    top_sector = max(sectors.items(), key=lambda s: s[1])
    hhi = sum((w / 100) ** 2 for _, w, _ in positions)  # Herfindahl index, 0..1

    score = 1.0
    score += min(4.0, max(0.0, (top_name[1] - 15) / 5))      # single-name concentration
    score += min(3.0, max(0.0, (top_sector[1] - 35) / 10))   # sector concentration
    score += 2.0 if len(positions) < 5 else 0.0              # too few positions
    score = round(min(10.0, score), 1)

    print(json.dumps({
        "score": score,
        "level": "low" if score <= 3 else "medium" if score <= 6 else "high",
        "largest_position": {"symbol": top_name[0], "weight_pct": top_name[1]},
        "largest_sector": {"sector": top_sector[0], "weight_pct": round(top_sector[1], 2)},
        "sector_weights": {k: round(v, 2) for k, v in sectors.items()},
        "herfindahl": round(hhi, 3),
        "positions": len(positions),
    }, indent=2))


if __name__ == "__main__":
    main()
