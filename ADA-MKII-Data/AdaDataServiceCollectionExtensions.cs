using ADA_MKII_Core.Abstractions;
using ADA_MKII_Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_Data;

/// <summary>Data composition root. Composed only by ADA-MKII-Server.</summary>
public static class AdaDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF context and the SQL-backed stores. The connection string
    /// comes from configuration - never hardcode it, and never commit it.
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

        return services;
    }
}
