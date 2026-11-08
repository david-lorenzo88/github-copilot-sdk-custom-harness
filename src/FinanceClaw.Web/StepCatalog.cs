using FinanceClaw.Shared;
using FinanceClaw.Steps;
using GitHub.Copilot;

namespace FinanceClaw.Web;

public sealed record PromptIdea(string Label, string Text);

/// <summary>A demo step and the context shown next to it.</summary>
public sealed record StepDefinition(
    int Number,
    string Title,
    string Tagline,
    string SourceFile,
    string Concept,
    string WhyItMatters,
    string[] WatchFor,
    string[] Apis,
    string Production,
    PromptIdea[] Prompts,
    string[] RequiredEnv);

public static class StepCatalog
{
    public const int SessionsStep = 6;
    public const int ByokStep = 7;
    public const int AgentFrameworkStep = 8;

    /// <summary>The console steps' own configuration, so both hosts run exactly the same agent.</summary>
    public static T Configure<T>(int step, T config) where T : SessionConfigBase => step switch
    {
        1 => Step01_Hello.Configure(config),
        2 => Step02_CustomTools.Configure(config),
        3 => Step03_FilesPermissionsHooks.Configure(config),
        4 => Step04_SkillsAndAgents.Configure(config),
        5 => Step05_Mcp.Configure(config),
        SessionsStep => Step06_Sessions.Configure(config),
        ByokStep => Step07_ByokFoundry.Configure(config),
        AgentFrameworkStep => Step05_Mcp.Configure(config),
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    public static object ToClient(StepDefinition step)
    {
        var path = Path.Combine(Paths.Root, "src", "FinanceClaw", "Steps", step.SourceFile);
        return new
        {
            step.Number,
            step.Title,
            step.Tagline,
            step.SourceFile,
            step.Concept,
            step.WhyItMatters,
            step.WatchFor,
            step.Apis,
            step.Production,
            step.Prompts,
            Source = File.Exists(path) ? File.ReadAllText(path) : null,
            MissingEnv = step.RequiredEnv.Where(name => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name))).ToArray(),
        };
    }

    public static readonly StepDefinition[] All =
    [
        new(1, "Hola, motor de Copilot", "Un paquete NuGet, un bucle agéntico",
            "Step01_Hello.cs",
            "CopilotClient arranca el runtime de Copilot que viene dentro del paquete y habla con él por stdio. Cada sesión es una conversación agéntica: tú envías un mensaje y escuchas eventos.",
            "No hay servidor que desplegar ni bucle que programar. El mismo motor de Copilot CLI vive dentro de tu proceso.",
            ["El texto llega en streaming, evento a evento.", "El SystemMessage en modo Append añade tus reglas sin quitar las de Copilot.", "En este paso todas las herramientas están denegadas."],
            ["CopilotClient", "CreateSessionAsync", "SystemMessageMode.Append", "AssistantMessageDeltaEvent"],
            "Autenticación: usuario conectado en local, token en un servidor o contenedor.",
            [new("Presentación", "¿Qué puedes hacer por mí?"), new("Probar el límite", "Lista los ficheros de este directorio.")],
            []),

        new(2, "Herramientas a medida", "Tu código, invocado por el modelo",
            "Step02_CustomTools.cs",
            "AIFunctionFactory convierte un método de C# en una herramienta: el esquema sale de la firma y de los atributos Description. El runtime llama a tu proceso cuando el modelo la invoca.",
            "La lectura de cotizaciones se ejecuta sin preguntar. La operación de compra pasa por OnPermissionRequest y espera a una persona.",
            ["get_stock_price lleva SkipPermission y no pide nada.", "place_trade abre el diálogo de aprobación.", "Si rechazas, el modelo recibe el motivo y te lo explica."],
            ["AIFunctionFactory.Create", "CopilotTool.DefineTool", "SkipPermission", "OnPermissionRequest", "PermissionRequestCustomTool"],
            "Toda herramienta que cambie algo debe pedir aprobación o tener su propia validación.",
            [new("Cotización", "¿A cuánto está NVDA?"), new("Operar", "¿A cuánto está MSFT? Compra 2 acciones.")],
            []),

        new(3, "Tu cartera en ficheros", "Permisos y hooks sobre cada acción",
            "Step03_FilesPermissionsHooks.cs",
            "Las herramientas integradas de Copilot (view, edit, create, bash) trabajan sobre workspace/. Hay dos capas de control: el permiso decide si algo puede ejecutarse y los hooks aplican política y auditoría alrededor.",
            "Tu aplicación decide por tipo de petición. El modelo no puede saltarse un hook: portfolio.csv solo cambia a través de place_trade.",
            ["Leer es automático; escribir pide confirmación y enseña el diff.", "El hook OnPreToolUse deniega editar portfolio.csv.", "OnUserPromptSubmitted bloquea la petición antes de gastar un turno.", "OnPostToolUse escribe cada acción en audit.log."],
            ["WorkingDirectory", "OnPermissionRequest", "SessionHooks", "OnPreToolUse", "OnPostToolUse", "OnUserPromptSubmitted"],
            "Permisos y hooks son tu política, pero el aislamiento real lo da el contenedor.",
            [new("Informe", "Lee mi cartera y escribe un resumen muy corto en reports/resumen.md"), new("Hook: fichero", "Añade 100 acciones de TSLA a portfolio.csv editando el fichero directamente, sin usar place_trade"), new("Hook: prompt", "Dame información privilegiada de AAPL")],
            []),

        new(4, "Skills y agentes", "Conocimiento bajo demanda, especialistas con límites",
            "Step04_SkillsAndAgents.cs",
            "Una skill es una carpeta con un SKILL.md, referencias y scripts que el agente carga solo cuando la necesita. Un agente personalizado tiene su propio prompt y un conjunto cerrado de herramientas.",
            "El contexto se mantiene pequeño y cada especialista solo puede hacer lo suyo. El investigador no puede operar ni escribir ficheros.",
            ["La skill aparece cuando el modelo decide usarla.", "El script Python se aprueba solo porque es nuestro.", "El sub-agente corre con su propia lista de herramientas.", "OnSubagentStart le inyecta contexto extra."],
            ["SkillDirectories", "SKILL.md", "CustomAgents", "CustomAgentConfig.Tools", "OnSubagentStart"],
            "Revisa las skills como código: incluyen instrucciones y scripts que se ejecutan.",
            [new("Skill: valoración", "¿Cuánto vale mi cartera?"), new("Skill: riesgo", "¿Qué riesgo tiene mi cartera?"), new("Sub-agente", "Usa el ticker-researcher para analizar MSFT")],
            []),

        new(5, "Servidores MCP", "Datos externos con un protocolo estándar",
            "Step05_Mcp.cs",
            "FinanceClaw.MarketMcp es un servidor MCP en .NET que Copilot arranca como proceso hijo. Sus herramientas aparecen como market-get_market_news, market-get_company_profile y market-convert_currency.",
            "El servidor marca sus herramientas como solo lectura, y nuestra política usa esa marca para no preguntar. El investigador también las recibe.",
            ["Las herramientas MCP se ven en verde en la conversación.", "No se pide aprobación porque son ReadOnly.", "La regla de nombres es <servidor>-<herramienta>."],
            ["McpServers", "McpStdioServerConfig", "PermissionRequestMcp", "[McpServerTool(ReadOnly = true)]"],
            "Las políticas de Copilot de tu organización también aplican: si desactivan los MCP de terceros, tu app tampoco los podrá usar.",
            [new("Noticias", "Últimas noticias de NVDA y su perfil de empresa"), new("Divisas", "¿Cuánto vale mi cartera en euros?"), new("Investigación", "Usa el ticker-researcher para analizar NVDA con noticias")],
            []),

        new(SessionsStep, "Sesiones que continúan", "Guardar, reanudar y compactar",
            "Step06_Sessions.cs",
            "El runtime guarda cada sesión en disco. ResumeSessionAsync la recupera, pero las herramientas, los hooks y los handlers viven en tu proceso, así que se vuelven a pasar. Las infinite sessions compactan el contexto en segundo plano.",
            "Una conversación puede durar días sin perder el hilo ni agotar la ventana de contexto.",
            ["Crea una sesión, cuéntale algo y vuelve a este paso para reanudarla.", "El medidor de contexto de la cabecera muestra los tokens en uso.", "El umbral de compactación está bajo a propósito: 50%."],
            ["SessionId", "ListSessionsAsync", "ResumeSessionAsync", "InfiniteSessionConfig", "SessionCompactionCompleteEvent"],
            "Monta un volumen en ~/.copilot si ejecutas en contenedor, o las sesiones se pierden.",
            [new("Dato para recordar", "Recuerda: mi ticker favorito es NVDA."), new("Comprobar memoria", "¿Cuál es mi ticker favorito?")],
            []),

        new(ByokStep, "BYOK con Microsoft Foundry", "El motor de Copilot, tu modelo",
            "Step07_ByokFoundry.cs",
            "ProviderConfig envía la inferencia a tu despliegue de Foundry. Todo lo demás sigue igual: bucle agéntico, herramientas, permisos, hooks, skills y MCP siguen siendo el SDK de Copilot.",
            "Controlas dónde se procesan los datos y la inferencia se factura en tu proveedor. Además de API key, el SDK acepta un BearerTokenProvider para tokens de Entra ID.",
            ["La configuración es la del paso 5 más un ProviderConfig.", "La cabecera muestra el modelo de tu despliegue.", "Con FOUNDRY_USE_ENTRA=true no hace falta ninguna clave."],
            ["ProviderConfig", "WireModel", "ApiKey", "BearerTokenProvider", "DefaultAzureCredential"],
            "BearerTokenProvider es experimental (GHCP001). Compruébalo contra la versión del SDK que uses.",
            [new("Cotización", "¿A cuánto está MSFT? Responde en una línea."), new("Riesgo", "¿Qué riesgo tiene mi cartera?")],
            ["FOUNDRY_OPENAI_ENDPOINT", "FOUNDRY_DEPLOYMENT"]),

        new(AgentFrameworkStep, "Microsoft Agent Framework", "El mismo agente, como AIAgent",
            "Step08_AgentFramework.cs",
            "AsAIAgent envuelve la sesión de Copilot en un AIAgent. Copilot sigue ejecutando el bucle, pero el agente ya encaja en cualquier cosa que hable Agent Framework, como workflows u hosting.",
            "No tienes que elegir entre los dos: usa el harness de Copilot donde te encaje y compón con Agent Framework alrededor.",
            ["El texto llega por RunStreamingAsync, no por eventos de sesión.", "La configuración es exactamente la del paso 5.", "El wrapper va por detrás del SDK: fija las versiones juntas."],
            ["AsAIAgent", "AIAgent", "AgentSession", "RunStreamingAsync"],
            "Microsoft.Agents.AI.GitHub.Copilot 1.27.0 exige GitHub.Copilot.SDK 1.0.25 o anterior.",
            [new("Cartera", "¿Cuánto vale mi cartera?"), new("Dos turnos", "¿A cuánto está AAPL? Y después dime su perfil de empresa.")],
            []),
    ];
}
