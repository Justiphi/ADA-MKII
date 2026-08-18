using System.Net;
using System.Net.Http.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>Client-side <see cref="INoteStore"/>, mirroring SqlNoteStore over HTTP.</summary>
public sealed class HttpNoteStore(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), INoteStore
{
    public async Task<NoteDto> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAsync(HttpMethod.Post, ApiRoutes.Notes.Base, request, cancellationToken);
        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no note.");
    }

    public async Task<NoteDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, ApiRoutes.Notes.ById(id), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<NoteDto>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Notes.Base}?limit={limit}",
            null,
            cancellationToken);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<NoteDto>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<NoteDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await ListAsync(limit, cancellationToken);
        }

        using var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Notes.Search(query)}&limit={limit}",
            null,
            cancellationToken);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<NoteDto>>(cancellationToken) ?? [];
    }

    public async Task<NoteDto?> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAsync(HttpMethod.Put, ApiRoutes.Notes.ById(id), request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, ApiRoutes.Notes.ById(id), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return true;
    }
}
