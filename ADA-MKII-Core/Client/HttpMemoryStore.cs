using System.Net;
using System.Net.Http.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>Client-side <see cref="IMemoryStore"/>, mirroring SqlMemoryStore over HTTP.</summary>
public sealed class HttpMemoryStore(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), IMemoryStore
{
    public async Task<MemoryDto> CreateAsync(CreateMemoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAsync(HttpMethod.Post, ApiRoutes.Memories.Base, request, cancellationToken);
        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<MemoryDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no memory.");
    }

    public async Task<IReadOnlyList<MemoryDto>> ListRecentAsync(int limit, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Memories.Base}?limit={limit}",
            null,
            cancellationToken);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<MemoryDto>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<MemoryDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await ListRecentAsync(limit, cancellationToken);
        }

        using var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Memories.Search(query)}&limit={limit}",
            null,
            cancellationToken);

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<MemoryDto>>(cancellationToken) ?? [];
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, ApiRoutes.Memories.ById(id), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await HttpConversationStore.EnsureSuccessAsync(response, cancellationToken);
        return true;
    }
}
