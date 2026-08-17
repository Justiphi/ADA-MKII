namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Token accounting, used by the spend guard. Separate from
/// <see cref="IConversationStore"/> because it answers an aggregate question and
/// wants a very different query.
/// </summary>
public interface IUsageStore
{
    /// <summary>Total prompt + completion tokens recorded since the given instant.</summary>
    Task<long> GetTokensSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
