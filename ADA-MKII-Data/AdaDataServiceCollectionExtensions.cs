using ADA_MKII_Core.Abstractions;
using ADA_MKII_Data.Stores;
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

        return services;
    }

    /// <summary>
    /// Registers only what an administrative tool needs: the context and the
    /// account store. Deliberately omits the account-scoped stores, because
    /// ADA-MKII-DataManager acts on every account rather than as one of them.
    /// </summary>
    public static IServiceCollection AddAdaDataAdmin(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AdaDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IAccountStore, SqlAccountStore>();
        services.AddScoped<IDeviceTokenStore, SqlDeviceTokenStore>();

        return services;
    }
}
