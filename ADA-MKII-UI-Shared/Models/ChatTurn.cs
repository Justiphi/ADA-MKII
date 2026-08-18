using ADA_MKII_Core.Contracts;

namespace ADA_MKII_UI_Shared.Models;

/// <summary>
/// A message as the UI needs it. Distinct from <see cref="ChatMessageDto"/>
/// because the UI must render a message that does not exist server-side yet -
/// the assistant reply while it is still streaming in.
/// </summary>
public sealed class ChatTurn
{
    public required ChatRole Role { get; init; }

    public string Content { get; set; } = string.Empty;

    /// <summary>True while deltas are still arriving, so the view can show a caret.</summary>
    public bool IsStreaming { get; set; }

    /// <summary>
    /// The tool ADA is running right now, or null. Transient and never persisted:
    /// it exists so the pause while a tool runs is explained rather than looking
    /// like the reply has stalled.
    /// </summary>
    public string? ActiveTool { get; set; }

    public int TokensIn { get; set; }

    public int TokensOut { get; set; }

    public static ChatTurn FromDto(ChatMessageDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ChatTurn
        {
            Role = dto.Role,
            Content = dto.Content,
            TokensIn = dto.TokensIn,
            TokensOut = dto.TokensOut,
        };
    }
}
