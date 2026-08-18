using System.Net;
using System.Net.Http.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>Client-side <see cref="ICalendarStore"/>, mirroring SqlCalendarStore over HTTP.</summary>
public sealed class HttpCalendarStore(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), ICalendarStore
{
    public async Task<CalendarEventDto> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAsync(HttpMethod.Post, ApiRoutes.Calendar.Base, request, cancellationToken);
        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<CalendarEventDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no event.");
    }

    public async Task<CalendarEventDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, ApiRoutes.Calendar.ById(id), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CalendarEventDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListSeriesAsync(int limit, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Calendar.Base}?limit={limit}",
            null,
            cancellationToken);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<CalendarEventDto>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<EventOccurrenceDto>> ListOccurrencesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            ApiRoutes.Calendar.Occurrences(fromUtc, toUtc),
            null,
            cancellationToken);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<EventOccurrenceDto>>(cancellationToken) ?? [];
    }

    public async Task<CalendarEventDto?> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAsync(HttpMethod.Put, ApiRoutes.Calendar.ById(id), request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CalendarEventDto>(cancellationToken);
    }

    public async Task<bool> CancelOccurrenceAsync(
        Guid eventId,
        DateTimeOffset occurrenceStartUtc,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            ApiRoutes.Calendar.CancelOccurrence(eventId),
            new CancelOccurrenceRequest(occurrenceStartUtc),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, ApiRoutes.Calendar.ById(id), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return true;
    }
}
