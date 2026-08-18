using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Pipeline;
using ADA_MKII_Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_Core;

/// <summary>Core composition root. Composed by ADA-MKII-Server.</summary>
public static class AdaCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the assistant pipeline, the tool registry and the tools
    /// themselves. The intent dispatcher still slots in alongside these.
    /// </summary>
    public static IServiceCollection AddAdaCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);

        services.AddOptions<AssistantOptions>()
            .Bind(configuration.GetSection(AssistantOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IAssistantPipeline, AssistantPipeline>();

        // Scoped, because one HTTP request is one turn: the zone set at the top
        // of a turn must not leak into anyone else's.
        services.AddScoped<AssistantTurnContext>();
        services.AddScoped<IToolRegistry, ToolRegistry>();

        // The record tools live in Core, not in ADA-MKII-API, because they are
        // not outbound: each one is a thin adapter over a Core store abstraction
        // and reaches nothing off this machine. API stays what its name promises
        // - clients we call - and is where a weather or ElevenLabs tool belongs.
        services.AddScoped<IAssistantTool, CreateNoteTool>();
        services.AddScoped<IAssistantTool, SearchNotesTool>();
        services.AddScoped<IAssistantTool, CreateEventTool>();
        services.AddScoped<IAssistantTool, ListEventsTool>();
        services.AddScoped<IAssistantTool, RememberTool>();
        services.AddScoped<IAssistantTool, RecallTool>();

        return services;
    }
}
