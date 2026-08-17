using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Server.Endpoints;

/// <summary>Conversation CRUD. Routes come from <see cref="ApiRoutes"/> so the client cannot drift.</summary>
public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(ApiRoutes.Conversations.Base)
            .RequireAuthorization()
            .WithTags("Conversations");

        group.MapGet("/", async (IConversationStore store, CancellationToken ct, int limit = 50) =>
            Results.Ok(await store.ListAsync(limit, ct)));

        group.MapPost("/", async (CreateConversationRequest? request, IConversationStore store, CancellationToken ct) =>
        {
            var created = await store.CreateAsync(request?.Title, ct);
            return Results.Created(ApiRoutes.Conversations.ById(created.Id), created);
        });

        group.MapGet("/{id:guid}", async (Guid id, IConversationStore store, CancellationToken ct) =>
        {
            var conversation = await store.GetAsync(id, ct);
            return conversation is null ? Results.NotFound() : Results.Ok(conversation);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IConversationStore store, CancellationToken ct) =>
            await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        group.MapPost("/{id:guid}/messages", async (
            Guid id,
            AppendMessageRequest request,
            IConversationStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
            {
                return Results.Problem(
                    title: "Message content is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var message = await store.AppendMessageAsync(id, request, ct);
            return message is null ? Results.NotFound() : Results.Ok(message);
        });

        return app;
    }
}
