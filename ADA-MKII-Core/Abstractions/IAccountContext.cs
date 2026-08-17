namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// Who the current request belongs to.
///
/// This exists so the store interfaces stay free of an accountId parameter. That
/// matters: the client-side HTTP implementations have no business accepting one,
/// because the bearer token already identifies the account and letting a caller
/// pass an id would invite it to be the wrong one. The server-side stores resolve
/// the account from here instead, and scope every query to it.
/// </summary>
public interface IAccountContext
{
    /// <summary>The authenticated account. Throws if nothing is authenticated.</summary>
    Guid AccountId { get; }

    bool IsAuthenticated { get; }
}
