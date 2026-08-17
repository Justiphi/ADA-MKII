using ADA_MKII_Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ADA_MKII_Data.Stores;

/// <summary>
/// Token accounting for the spend guard. Sums the per-message counts recorded by
/// <see cref="SqlConversationStore"/>, which is why those columns exist from the
/// very first migration rather than being added once a bill surprised someone.
/// </summary>
public sealed class SqlUsageStore(AdaDbContext db) : IUsageStore
{
    public async Task<long> GetTokensSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        // Sum in the database rather than pulling rows back: this runs on every turn.
        return await db.Messages
            .AsNoTracking()
            .Where(m => m.CreatedUtc >= since)
            .SumAsync(m => (long)m.TokensIn + m.TokensOut, cancellationToken);
    }
}
