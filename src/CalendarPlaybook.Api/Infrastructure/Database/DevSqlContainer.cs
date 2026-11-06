using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace CalendarPlaybook.Api.Infrastructure.Database;

/// <summary>
/// Development only: starts the local SQL Server Podman container named by <c>DevSqlContainer:Name</c>
/// and waits until it accepts connections. The container itself is created once with <c>podman run</c>.
/// </summary>
public static class DevSqlContainer
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    public static void EnsureRunning(WebApplication app)
    {
        var name = app.Configuration["DevSqlContainer:Name"];
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        // Runs podman start calendar-sql, then waits up to 60 s for SQL Server to accept connections. 
        // Connects to master because the Calendar database doesn’t exist before the first migration.
        if (Start(name, app.Logger))
        {
            WaitUntilReady(app.Configuration.GetConnectionString(DatabaseSetup.ConnectionStringName), app.Logger);
        }
    }

    private static bool Start(string name, ILogger logger)
    {
        var startInfo = new ProcessStartInfo("podman")
        {
            ArgumentList = { "start", name },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        try
        {
            using var process = Process.Start(startInfo)!;
            var error = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(StartTimeout))
            {
                process.Kill();
                logger.LogWarning("podman start {Container} did not finish within {Timeout}", name, StartTimeout);
                return false;
            }

            if (process.ExitCode != 0)
            {
                logger.LogWarning("podman start {Container} failed: {Error}", name, error.Result.Trim());
                return false;
            }

            logger.LogInformation("SQL Server container {Container} is running", name);
            return true;
        }
        catch (Win32Exception)
        {
            logger.LogWarning("podman was not found on PATH; start the SQL Server container {Container} yourself", name);
            return false;
        }
    }

    /// <summary>Connects to <c>master</c>, since the app database may not exist before the first migration.</summary>
    private static void WaitUntilReady(string? connectionString, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", ConnectTimeout = 5 }.ConnectionString;
        var deadline = DateTime.UtcNow + ReadyTimeout;

        while (true)
        {
            try
            {
                using var connection = new SqlConnection(master);
                connection.Open();
                return;
            }
            catch (SqlException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
            catch (SqlException exception)
            {
                // Let the migration report the real error (e.g. a wrong password).
                logger.LogWarning("SQL Server was not ready within {Timeout}: {Error}", ReadyTimeout, exception.Message);
                return;
            }
        }
    }
}
