namespace ADA_MKII_Core.Contracts;

/// <summary>
/// A single message on the wire. Token counts are persisted per message so the
/// spend guard in <see cref="SettingKeys.MonthlyBudgetUsd"/> has something to
/// measure - see CLAUDE.md on cost control.
/// </summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    ChatRole Role,
    string Content,
    DateTimeOffset CreatedUtc,
    int TokensIn,
    int TokensOut);
