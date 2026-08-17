using System.IO;
using System.Windows;
using ADA_MKII_Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ADA_MKII_DataManager;

/// <summary>
/// Composition root for the operator tool. Connects straight to SQL Server, so
/// run it only where that is safe - on the VPS, or over a VPN or SSH tunnel.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<App>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Ada");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail with something an operator can act on, rather than a null
            // reference three layers down.
            MessageBox.Show(
                "No 'Ada' connection string is configured.\n\n" +
                "Set it with:\n" +
                "  dotnet user-secrets set \"ConnectionStrings:Ada\" \"<connection string>\" --project ADA-MKII-DataManager",
                "ADA DataManager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAdaDataAdmin(connectionString);
        services.AddSingleton<AdminService>();

        _services = services.BuildServiceProvider();

        var window = new MainWindow(_services.GetRequiredService<AdminService>());
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
