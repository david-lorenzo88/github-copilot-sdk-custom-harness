using FinanceClaw.Shared;
using FinanceClaw.Steps;

Dictionary<string, (string Title, Func<Task> Run)> steps = new()
{
    ["1"] = ("Hola Copilot SDK", Step01_Hello.RunAsync),
    ["2"] = ("Herramientas a medida (AIFunctionFactory)", Step02_CustomTools.RunAsync),
    ["3"] = ("Cartera en ficheros: permisos y hooks", Step03_FilesPermissionsHooks.RunAsync),
    ["4"] = ("Skills y agentes personalizados", Step04_SkillsAndAgents.RunAsync),
    ["5"] = ("Servidores MCP", Step05_Mcp.RunAsync),
    ["6"] = ("Sesiones persistentes e infinitas", Step06_Sessions.RunAsync),
    ["7"] = ("BYOK con Microsoft Foundry", Step07_ByokFoundry.RunAsync),
    ["8"] = ("Microsoft Agent Framework", Step08_AgentFramework.RunAsync),
};

var choice = args.FirstOrDefault();
if (choice is null || !steps.ContainsKey(choice))
{
    Ui.Header("Finance Claw · GitHub Copilot SDK para .NET");
    foreach (var (key, step) in steps)
        Console.WriteLine($"  {key}. {step.Title}");
    Console.Write("\nPaso: ");
    choice = Console.ReadLine()?.Trim();
}

if (choice is null || !steps.TryGetValue(choice, out var selected))
{
    Ui.Error("Paso desconocido.");
    return 1;
}

await selected.Run();
return 0;
