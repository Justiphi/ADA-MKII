using ADA_MKII_Core;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Server.Endpoints;

/// <summary>
/// Non-secret user preferences. There is deliberately no endpoint that exposes
/// or accepts an API key: provider credentials live in server configuration only.
/// </summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(ApiRoutes.Settings.Base)
            .RequireAuthorization()
            .WithTags("Settings");

        group.MapGet("/", async (ISettingsStore store, CancellationToken ct) =>
            Results.Ok(await store.ListAsync(ct)));

        group.MapGet("/{key}", async (string key, ISettingsStore store, CancellationToken ct) =>
        {
            var value = await store.GetAsync<string>(key, ct);
            return value is null ? Results.NotFound() : Results.Ok(new SettingDto(key, value));
        });

        group.MapPut("/{key}", async (string key, SetSettingRequest request, ISettingsStore store, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.Problem(title: "A value is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            await store.SetAsync(key, request.Value, ct);
            return Results.Ok(new SettingDto(key, request.Value));
        });

        return app;
    }
}
