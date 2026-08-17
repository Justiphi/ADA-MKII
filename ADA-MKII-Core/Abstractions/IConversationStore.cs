using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Conversation persistence. Deliberately has two implementations: SqlConversationStore
/// in ADA-MKII-Data (server side, EF Core) and the HTTP client in this assembly
/// (client side). That symmetry is what lets ADA-MKII-UI-Shared be identical on
/// every head - see CLAUDE.md.
/// </summary>
public interface IConversationStore
{
    Task<ConversationDto> CreateAsync(string? title, CancellationToken cancellationToken);

    Task<ConversationDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationDto>> ListAsync(int limit, CancellationToken cancellationToken);

    Task<ChatMessageDto?> AppendMessageAsync(Guid conversationId, AppendMessageRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
