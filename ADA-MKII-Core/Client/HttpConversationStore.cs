using System.Net;
using System.Net.Http.Json;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Client;

/// <summary>
/// Client-side <see cref="IConversationStore"/>, talking to ADA-MKII-Server over
/// HTTPS. Deliberately the same interface as SqlConversationStore: UI code binds
/// to the abstraction and neither knows nor cares which side of the wire it is on.
///
/// Note there is no account id anywhere in these calls - the bearer token says
/// who we are, and the server scopes accordingly.
/// </summary>
public sealed class HttpConversationStore(HttpClient http, ISessionStore session)
    : AuthenticatedHttpClient(http, session), IConversationStore
{
    public async Task<ConversationDto> CreateAsync(string? title, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            ApiRoutes.Conversations.Base,
            new CreateConversationRequest(title),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ConversationDto>(cancellationToken)
            ?? throw new AdaApiException(response.StatusCode, "Server returned an empty conversation.");
    }

    public async Task<ConversationDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, ApiRoutes.Conversations.ById(id), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ConversationDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationDto>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Conversations.Base}?limit={limit}",
            null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ConversationDto>>(cancellationToken) ?? [];
    }

    public async Task<ChatMessageDto?> AppendMessageAsync(
        Guid conversationId,
        AppendMessageRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            ApiRoutes.Conversations.Messages(conversationId),
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChatMessageDto>(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, ApiRoutes.Conversations.ById(id), null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // The server speaks ProblemDetails; surface its title rather than a bare
        // status code, but never assume the body parsed cleanly.
        string? detail = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(cancellationToken);
            detail = problem?.Title ?? problem?.Detail;
        }
        catch (System.Text.Json.JsonException)
        {
            // Not a ProblemDetails payload - fall through to the status code.
        }

        throw new AdaApiException(
            response.StatusCode,
            detail ?? $"Request failed with status {(int)response.StatusCode}.");
    }

    private sealed record ProblemDetailsDto(string? Title, string? Detail, int? Status);
}
