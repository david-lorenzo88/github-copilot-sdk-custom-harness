using Azure.Core;
using Azure.Identity;
using FinanceClaw.Shared;
using GitHub.Copilot;

namespace FinanceClaw.Steps;

/// <summary>
/// Paso 7 · BYOK con Microsoft Foundry.
/// Same agent, same tools, but inference goes to your own Foundry deployment.
///
///   FOUNDRY_OPENAI_ENDPOINT  https://&lt;resource&gt;.openai.azure.com/openai/v1
///   FOUNDRY_DEPLOYMENT       your deployment name (e.g. gpt-5-mini)
///   FOUNDRY_MODEL            well-known model id, if it differs from the deployment name
///   FOUNDRY_API_KEY          API key   — or —
///   FOUNDRY_USE_ENTRA=true   bearer token per request via DefaultAzureCredential
/// </summary>
public static class Step07_ByokFoundry
{
    private static readonly string[] CognitiveServicesScope = ["https://cognitiveservices.azure.com/.default"];

    public static T Configure<T>(T config) where T : SessionConfigBase
    {
        Step05_Mcp.Configure(config);

        var deployment = Required("FOUNDRY_DEPLOYMENT");
        var provider = new ProviderConfig
        {
            Type = "openai",
            WireApi = "responses",
            ModelProvider = "azure_openai",
            BaseUrl = Required("FOUNDRY_OPENAI_ENDPOINT"),
            ModelId = Environment.GetEnvironmentVariable("FOUNDRY_MODEL") ?? deployment,
            WireModel = deployment,
        };

        if (Environment.GetEnvironmentVariable("FOUNDRY_USE_ENTRA") == "true")
        {
            // The SDK takes no identity dependency: we hand it a callback and it asks per request.
            var credential = new DefaultAzureCredential();
            provider.BearerTokenProvider = async _ =>
                (await credential.GetTokenAsync(new TokenRequestContext(CognitiveServicesScope))).Token;
        }
        else
        {
            provider.ApiKey = Required("FOUNDRY_API_KEY");
        }

        config.Provider = provider;
        config.Model = deployment;
        return config;
    }

    public static async Task RunAsync()
    {
        await using var client = await ClawClient.StartAsync();
        await using var session = await client.CreateSessionAsync(Configure(new SessionConfig()));

        await ConsoleChat.RunAsync(session, "Paso 7 · BYOK con Microsoft Foundry");
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {name} to run the BYOK step.");
}
