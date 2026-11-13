# Finance Claw · El motor de GitHub Copilot dentro de tu app

Demo de la sesión **"El motor de GitHub Copilot dentro de tu app: GitHub Copilot SDK para .NET"**.

Reconstruimos el asistente de finanzas personales de la serie oficial
[Build your own claw](https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework)
de Microsoft Agent Framework sobre el motor de Copilot ([`GitHub.Copilot.SDK`](https://www.nuget.org/packages/GitHub.Copilot.SDK)).
Tú pones las herramientas, las reglas y la experiencia; Copilot pone el bucle agéntico.

> Los datos de mercado son simulados y las operaciones no se envían a ningún bróker.

## Requisitos

- .NET 10 SDK
- Python 3 (lo usan los scripts de las skills)
- Una cuenta con GitHub Copilot. El paquete NuGet incluye el runtime de Copilot, pero no inicia sesión por ti:
  - inicia sesión una vez con [Copilot CLI](https://github.com/github/copilot-cli) (`brew install --cask copilot-cli` y luego `copilot login`), **o**
  - exporta `COPILOT_GITHUB_TOKEN` con un fine-grained PAT con el permiso *Copilot Requests*.
- Opcional, para el paso 7: un deployment de Microsoft Foundry.

```bash
dotnet build
dotnet run --project src/FinanceClaw        # muestra el menú
dotnet run --project src/FinanceClaw -- 3   # va directo al paso 3
```

`CLAW_MODEL` elige el modelo (por ejemplo `claude-sonnet-4.5` o `gpt-5`). Si no lo pones, el runtime usa su modelo por defecto.

## Estructura

```
src/FinanceClaw/
  Program.cs                       menú de pasos
  Tools/FinanceTools.cs            get_stock_price y place_trade (AIFunctionFactory)
  Shared/Approvals.cs              OnPermissionRequest por tipo de petición
  Shared/AuditHooks.cs             OnPreToolUse, OnPostToolUse, OnUserPromptSubmitted…
  Shared/ConsoleChat.cs            REPL y eventos (tools, skills, sub-agentes, compactación)
  Steps/Step01..Step08             un fichero por paso; cada paso extiende la configuración del anterior
src/FinanceClaw.MarketMcp/         servidor MCP stdio (noticias, perfil de empresa, divisas)
src/FinanceClaw.Web/               la misma demo en el navegador (ASP.NET Core + eventos en directo)
skills/valuation, skills/risk-scoring   SKILL.md + references + script Python
workspace/                         directorio de trabajo del agente (portfolio.csv, reports/, confirmations/)
Dockerfile                         el claw en un contenedor
```

Cada paso expone `Configure<T>(T config) where T : SessionConfigBase`, que llama al paso anterior y añade una sola cosa.
En directo puedes enseñar solo la diferencia de cada fichero.

## Guion de la demo

| Paso | Qué enseña | Prompt para probarlo |
|---|---|---|
| 1 · Hola Copilot SDK | `CopilotClient`, `CreateSessionAsync`, streaming, `SystemMessage` en modo Append | `¿Qué puedes hacer por mí?` |
| 2 · Herramientas a medida | `AIFunctionFactory.Create`, `CopilotTool.DefineTool` con `SkipPermission`, aprobación de `place_trade` en `OnPermissionRequest` | `¿A cuánto está NVDA? Compra 5 acciones.` |
| 3 · Cartera en ficheros | herramientas integradas en `workspace/`, permisos por tipo (`Read`/`Write`/`Shell`/`CustomTool`), hooks que bloquean, auditan y guardan confirmaciones | `Lee mi cartera y escribe un resumen en reports/` · `Añade 100 TSLA a portfolio.csv` (lo bloquea el hook) · `Dame información privilegiada de AAPL` (lo bloquea `OnUserPromptSubmitted`) |
| 4 · Skills y agentes | `SkillDirectories`, `CustomAgents` (ticker-researcher), `OnSubagentStart` | `¿Cuánto vale mi cartera?` (skill valuation) · `¿Qué riesgo tengo?` (skill risk-scoring) · `Usa el ticker-researcher para analizar MSFT` |
| 5 · MCP | `McpServers` con un servidor stdio; las herramientas se llaman `market-<tool>`; las de solo lectura se aprueban solas | `Últimas noticias de NVDA y su perfil` · `¿Cuánto vale mi cartera en euros?` |
| 6 · Sesiones | `SessionId`, `ListSessionsAsync`, `ResumeSessionAsync`, `InfiniteSessions` y eventos de compactación | Haz una pregunta, sal con `exit`, vuelve a ejecutar el paso 6 y elige la sesión |
| 7 · BYOK con Foundry | `ProviderConfig` con API key o con `BearerTokenProvider` (Entra ID) | Cualquiera de los anteriores |
| 8 · Agent Framework | `copilotClient.AsAIAgent(...)` → `AIAgent`, `RunStreamingAsync` | `¿Cuánto vale mi cartera?` |

Después de los pasos 3–5, mira `workspace/audit.log` (lo escribe `OnPostToolUse`) y `workspace/confirmations/` (un fichero por operación aprobada).

### Paso 7 · variables de entorno

```bash
export FOUNDRY_OPENAI_ENDPOINT="https://<recurso>.openai.azure.com/openai/v1"
export FOUNDRY_DEPLOYMENT="gpt-5-mini"
export FOUNDRY_API_KEY="..."          # o bien:
export FOUNDRY_USE_ENTRA=true         # token por petición con DefaultAzureCredential (az login, identidad administrada…)
dotnet run --project src/FinanceClaw -- 7
```

## Interfaz web

La misma demo, en el navegador y con estética neón. Cada paso usa exactamente la configuración de la versión de consola.

```bash
dotnet run --project src/FinanceClaw.Web
```

Abre http://localhost:5199. Las teclas `1`–`8` cambian de paso, y `S` / `N` responden a las aprobaciones (`Y` / `N` en inglés). El selector `ES | EN` de la cabecera cambia el idioma de la interfaz sin cortar la sesión y el navegador lo recuerda.

- **Conversación en directo:** texto en streaming y herramientas por colores (tu código, integradas, MCP), skills y sub-agentes.
- **Aprobaciones en un diálogo:** ticket de operación, comando o diff del fichero. Sin respuesta en 5 minutos se rechaza.
- **Hooks visibles:** cada bloqueo se ve en pantalla y queda en la pestaña Actividad.
- **Contexto de cada paso:** concepto, por qué importa, en qué fijarse, APIs, el código real del paso y la cartera.
- **Medidor de contexto:** tokens en uso y animación cuando se compacta.

El paso 7 lee los ajustes de Foundry del fichero `.env` de la raíz. El servidor solo escucha en `localhost` y rechaza peticiones de otros orígenes, porque quien llegue a él puede aprobar operaciones y comandos.

## En un contenedor

El agente lee, escribe y ejecuta comandos. Lo sensato es darle un sistema de ficheros desechable y un usuario sin privilegios:

```bash
docker build -t finance-claw .
docker run -it --rm -e COPILOT_GITHUB_TOKEN=$COPILOT_GITHUB_TOKEN -v claw-sessions:/home/claw/.copilot finance-claw 5
```

## Antes de llevarlo a producción

- **Suscripción y facturación.** Con autenticación de GitHub, cada sesión consume la suscripción de Copilot del usuario o de la organización (peticiones premium según el modelo). Con BYOK, la inferencia se factura en tu proveedor.
- **Versiones.** El proyecto fija `GitHub.Copilot.SDK` 1.0.25. `Microsoft.Agents.AI.GitHub.Copilot` 1.27.0 está compilado contra una versión del SDK que todavía tenía `SessionConfigBase.RemoteSession`. Esa propiedad desaparece en la 1.0.26, y con un SDK más nuevo el paso 8 falla en el segundo turno con `MissingMethodException`. Sube las dos versiones a la vez.
- **Políticas de Copilot.** El SDK respeta las mismas políticas que Copilot CLI. Si tu organización desactiva Copilot CLI o los servidores MCP de terceros, tu app tampoco puede usarlos (error 403 "not authorized to use this Copilot feature"). Si cambias de plan, vuelve a iniciar sesión para refrescar el token.
- **BYOK y credenciales.** Desde la versión 1.0.25 del SDK, `ProviderConfig` admite `ApiKey`, `BearerToken` y `BearerTokenProvider`, un callback que se llama en cada petición y sirve para tokens de Entra ID o de identidad administrada (el paso 7 lo usa con `DefaultAzureCredential`). Revisa este punto contra la versión que enseñes, porque cambia rápido.
- **Contenedor.** Las herramientas integradas trabajan sobre el sistema de ficheros y la shell del proceso. Los permisos y los hooks son tu política, pero el aislamiento real lo da el contenedor.
- **APIs experimentales.** Varias APIs (por ejemplo `PermissionDecision` y `BearerTokenProvider`) llevan el diagnóstico `GHCP001`. El proyecto lo suprime en el `.csproj`.

## Harness de Copilot o harness propio

| | Copilot SDK (este repo) | Agent Framework harness (claw original) |
|---|---|---|
| Bucle agéntico, planificación, compactación | Lo pone Copilot | Lo compones tú (providers, middleware) |
| Herramientas de ficheros y shell | Integradas, las controlas con permisos y hooks | Las añades tú (`FileAccessProvider`, `LocalShellExecutor`) |
| Modelos | Catálogo de Copilot o BYOK | Cualquier `IChatClient` |
| Coste | Suscripción de Copilot (o tu proveedor con BYOK) | Tu proveedor de modelos |
| Cuándo elegirlo | Quieres el comportamiento de Copilot CLI dentro de tu app con poco código | Necesitas controlar cada pieza del bucle o el hosting |

Y no hay que elegir solo uno: el paso 8 expone el agente de Copilot como `AIAgent`, así que encaja en workflows de Agent Framework.

## Referencias

- [github/copilot-sdk](https://github.com/github/copilot-sdk)
- [Build your own claw: código de ejemplo](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness/BuildYourOwnClaw)
- [Microsoft.Agents.AI.GitHub.Copilot](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot)
