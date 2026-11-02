# Finance Claw in a container: the agent can read, write and run shell commands,
# so give it a disposable filesystem, a non-root user and nothing else from your machine.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# The GitHub.Copilot.SDK package downloads the Copilot runtime for the build RID (linux here).
RUN dotnet publish src/FinanceClaw -c Release -o /out/app \
 && dotnet publish src/FinanceClaw.MarketMcp -c Release -o /out/mcp

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /claw
COPY --from=build /out/app ./app
COPY --from=build /out/mcp ./mcp
COPY skills ./skills
COPY workspace ./workspace

RUN useradd --create-home claw && chown -R claw:claw /claw
USER claw

ENV CLAW_ROOT=/claw \
    MARKET_MCP_DLL=/claw/mcp/FinanceClaw.MarketMcp.dll
# Session state lives in ~/.copilot: mount a volume there to keep sessions across runs.
VOLUME ["/home/claw/.copilot"]

# Pass a token with Copilot access: docker run -it -e COPILOT_GITHUB_TOKEN=... finance-claw 5
ENTRYPOINT ["dotnet", "/claw/app/FinanceClaw.dll"]
