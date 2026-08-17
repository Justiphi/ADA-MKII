using System.Text.Json;
using System.Text.Json.Serialization;
using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Server.Logging;
using Microsoft.AspNetCore.Http.Features;

namespace ADA_MKII_Server.Endpoints;

/// <summary>
/// The chat stream. Server-sent events rather than SignalR: SSE is consumed
/// identically by HttpClient (Discord), a Blazor circuit, and browser fetch,
/// whereas SignalR would add a second real-time stack for no gain.
/// </summary>
public static class ChatEndpoints
{
    // Omit nulls: a ChatStreamEvent has seven fields but a delta populates one,
    // so writing the nulls would roughly quadruple the bytes on the hot path.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerOptions.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ApiRoutes.Chat, async (
            ChatRequest request,
            IAssistantPipeline pipeline,
            HttpContext http,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var response = http.Response;
            response.Headers.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers.Append("X-Accel-Buffering", "no");

            // Without this the deltas sit in the response buffer and the client
            // sees one lump at the end, which defeats the point of streaming.
            http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            try
            {
                await foreach (var evt in pipeline.RunAsync(request, cancellationToken))
                {
                    await WriteEventAsync(response, evt, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The client hung up. Nothing to report and nobody to report it
                // to; the pipeline has already stopped consuming the model.
            }
#pragma warning disable CA1031 // The stream has begun, so a thrown exception can
            // no longer become a ProblemDetails response. Surfacing it as a
            // terminal error event is the only way the client learns of it.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ServerLog.ChatStreamFailed(loggerFactory.CreateLogger("Ada.Chat"), ex);
                await WriteEventAsync(response, ChatStreamEvent.Failed("The assistant failed to complete this turn."), CancellationToken.None);
            }
        })
        .RequireAuthorization()
        .WithTags("Chat");

        return app;
    }

    private static async Task WriteEventAsync(HttpResponse response, ChatStreamEvent evt, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(evt, SerializerOptions);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
