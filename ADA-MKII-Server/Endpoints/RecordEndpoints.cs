using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Server.Endpoints;

/// <summary>
/// Notes, memories and calendar events.
///
/// All three follow the pattern set by <see cref="ConversationEndpoints"/>: an
/// authorised group, and <c>NotFound</c> for anything the current account does
/// not own — the stores scope by account, so a record belonging to someone else
/// is indistinguishable from one that does not exist. That is the intent.
/// </summary>
public static class RecordEndpoints
{
    public static IEndpointRouteBuilder MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(ApiRoutes.Notes.Base)
            .RequireAuthorization()
            .WithTags("Notes");

        group.MapGet("/", async (INoteStore store, CancellationToken ct, int limit = 50) =>
            Results.Ok(await store.ListAsync(limit, ct)));

        group.MapGet("/search", async (string q, INoteStore store, CancellationToken ct, int limit = 50) =>
            Results.Ok(await store.SearchAsync(q, limit, ct)));

        group.MapGet("/{id:guid}", async (Guid id, INoteStore store, CancellationToken ct) =>
        {
            var note = await store.GetAsync(id, ct);
            return note is null ? Results.NotFound() : Results.Ok(note);
        });

        group.MapPost("/", async (CreateNoteRequest request, INoteStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
            {
                return Results.Problem(title: "Note content is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var created = await store.CreateAsync(request, ct);
            return Results.Created(ApiRoutes.Notes.ById(created.Id), created);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest request, INoteStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
            {
                return Results.Problem(title: "Note content is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var updated = await store.UpdateAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id:guid}", async (Guid id, INoteStore store, CancellationToken ct) =>
            await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }

    public static IEndpointRouteBuilder MapMemoryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(ApiRoutes.Memories.Base)
            .RequireAuthorization()
            .WithTags("Memories");

        group.MapGet("/", async (IMemoryStore store, CancellationToken ct, int limit = 50) =>
            Results.Ok(await store.ListRecentAsync(limit, ct)));

        group.MapGet("/search", async (string q, IMemoryStore store, CancellationToken ct, int limit = 20) =>
            Results.Ok(await store.SearchAsync(q, limit, ct)));

        // Memories are normally written by ADA through a tool, but creating one
        // by hand is useful for seeding and for correcting what it got wrong.
        group.MapPost("/", async (CreateMemoryRequest request, IMemoryStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
            {
                return Results.Problem(title: "Memory content is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var created = await store.CreateAsync(request, ct);
            return Results.Created(ApiRoutes.Memories.ById(created.Id), created);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMemoryStore store, CancellationToken ct) =>
            await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }

    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(ApiRoutes.Calendar.Base)
            .RequireAuthorization()
            .WithTags("Calendar");

        group.MapGet("/", async (ICalendarStore store, CancellationToken ct, int limit = 100) =>
            Results.Ok(await store.ListSeriesAsync(limit, ct)));

        // The window is required rather than defaulted: expanding recurrence needs
        // a bound, and silently choosing one for the caller hides the cost.
        group.MapGet("/occurrences", async (
            DateTimeOffset from,
            DateTimeOffset to,
            ICalendarStore store,
            CancellationToken ct) =>
        {
            if (to <= from)
            {
                return Results.Problem(title: "'to' must be after 'from'.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (to - from > TimeSpan.FromDays(400))
            {
                return Results.Problem(
                    title: "Window is too large; request at most 400 days.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Ok(await store.ListOccurrencesAsync(from, to, ct));
        });

        group.MapGet("/{id:guid}", async (Guid id, ICalendarStore store, CancellationToken ct) =>
        {
            var found = await store.GetAsync(id, ct);
            return found is null ? Results.NotFound() : Results.Ok(found);
        });

        group.MapPost("/", async (CreateEventRequest request, ICalendarStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Title))
            {
                return Results.Problem(title: "Event title is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var created = await store.CreateAsync(request, ct);
            return Results.Created(ApiRoutes.Calendar.ById(created.Id), created);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest request, ICalendarStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Title))
            {
                return Results.Problem(title: "Event title is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var updated = await store.UpdateAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // Skipping one occurrence of a series, without disturbing the rest.
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelOccurrenceRequest request,
            ICalendarStore store,
            CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.Problem(title: "An occurrence start is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            return await store.CancelOccurrenceAsync(id, request.OccurrenceStartUtc, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ICalendarStore store, CancellationToken ct) =>
            await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
