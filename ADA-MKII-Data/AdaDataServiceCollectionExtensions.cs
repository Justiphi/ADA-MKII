using ADA_MKII_Core.Abstractions;
using ADA_MKII_Data.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_Data;

/// <summary>Data composition root. Composed by ADA-MKII-Server and ADA-MKII-DataManager.</summary>
public static class AdaDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF context and the SQL-backed stores. The connection string
    /// comes from configuration - never hardcode it, and never commit it.
    ///
    /// The conversation, settings and usage stores depend on
    /// <see cref="IAccountContext"/>, which the host must register: ADA-MKII-Server
    /// resolves it from the authenticated principal.
    /// </summary>
    public static IServiceCollection AddAdaData(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContextPool<AdaDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IConversationStore, SqlConversationStore>();
        services.AddScoped<ISettingsStore, SqlSettingsStore>();
        services.AddScoped<IDeviceTokenStore, SqlDeviceTokenStore>();
        services.AddScoped<IUsageStore, SqlUsageStore>();
        services.AddScoped<IAccountStore, SqlAccountStore>();
        services.AddScoped<INoteStore, SqlNoteStore>();
        services.AddScoped<IMemoryStore, SqlMemoryStore>();
        services.AddScoped<ICalendarStore, SqlCalendarStore>();

        return services;
    }

    /// <summary>
    /// Registers only what an administrative tool needs: the context and the
    /// account store. Deliberately omits the account-scoped stores, because
    /// ADA-MKII-DataManager acts on every account rather than as one of them.
    ///
    /// Two deliberate differences from <see cref="AddAdaData"/>, both because a
    /// human is sitting watching this one:
    ///   - No retry-on-failure. Retries are right for a server riding out a
    ///     transient fault, but here they turn an unreachable database into
    ///     minutes of frozen window instead of one quick, honest error.
    ///   - A short connect timeout, unless the caller set one. SqlClient's
    ///     default is 15 seconds per attempt, which already feels broken.
    /// </summary>
    public static IServiceCollection AddAdaDataAdmin(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AdaDbContext>(options =>
            options.UseSqlServer(WithFastFailure(connectionString), sql => sql.CommandTimeout(AdminCommandTimeoutSeconds)));

        services.AddScoped<IAccountStore, SqlAccountStore>();
        services.AddScoped<IDeviceTokenStore, SqlDeviceTokenStore>();

        return services;
    }

    private const int AdminConnectTimeoutSeconds = 5;
    private const int AdminCommandTimeoutSeconds = 15;

    private static string WithFastFailure(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        // 15 is SqlClient's default; treat that as "not deliberately chosen".
        if (builder.ConnectTimeout == 15)
        {
            builder.ConnectTimeout = AdminConnectTimeoutSeconds;
        }

        return builder.ConnectionString;
    }
}
