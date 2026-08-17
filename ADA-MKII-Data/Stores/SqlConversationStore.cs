using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>
/// Server-side <see cref="IConversationStore"/>. Returns Core DTOs, never EF
/// entities - the mapping boundary lives here and nowhere else.
///
/// Every query is filtered by <see cref="IAccountContext.AccountId"/>. Reading
/// the owner from the request context rather than from a parameter means a
/// caller cannot ask for someone else's conversation: there is no argument in
/// which to pass the wrong id.
/// </summary>
public sealed class SqlConversationStore(AdaDbContext db, IAccountContext account, TimeProvider clock)
    : IConversationStore
{
    public async Task<ConversationDto> CreateAsync(string? title, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var entity = new ConversationEntity
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.AccountId,
            Title = string.IsNullOrWhiteSpace(title) ? "New conversation" : title.Trim(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        db.Conversations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entity, []);
    }

    public async Task<ConversationDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id && c.AccountId == account.AccountId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var messages = entity.Messages
            .OrderBy(m => m.CreatedUtc)
            .Select(ToDto)
            .ToList();

        return ToDto(entity, messages);
    }

    public async Task<IReadOnlyList<ConversationDto>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit, 1, 200);

        var entities = await db.Conversations
            .AsNoTracking()
            .Where(c => c.AccountId == account.AccountId)
            .OrderByDescending(c => c.UpdatedUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        // Summaries only: message bodies are not loaded for a list view.
        return [.. entities.Select(e => ToDto(e, []))];
    }

    public async Task<ChatMessageDto?> AppendMessageAsync(
        Guid conversationId,
        AppendMessageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.AccountId == account.AccountId, cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var message = new MessageEntity
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Role = request.Role,
            Content = request.Content,
            CreatedUtc = now,
            TokensIn = request.TokensIn,
            TokensOut = request.TokensOut,
        };

        db.Messages.Add(message);
        conversation.UpdatedUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(message);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Conversations
            .Where(c => c.Id == id && c.AccountId == account.AccountId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    private static ConversationDto ToDto(ConversationEntity entity, IReadOnlyList<ChatMessageDto> messages) =>
        new(entity.Id, entity.Title, entity.CreatedUtc, entity.UpdatedUtc, messages);

    private static ChatMessageDto ToDto(MessageEntity entity) =>
        new(entity.Id, entity.ConversationId, entity.Role, entity.Content, entity.CreatedUtc, entity.TokensIn, entity.TokensOut);
}
