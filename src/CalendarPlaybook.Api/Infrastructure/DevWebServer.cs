using System.Diagnostics;

namespace CalendarPlaybook.Api.Infrastructure;

/// <summary>
/// Development only: runs <c>npm run dev</c> (Vite) in the folder named by <c>DevWebServer:Path</c>
/// alongside the API and stops it when the API shuts down.
/// </summary>
public static class DevWebServer
{
    public static void Start(WebApplication app)
    {
        var path = app.Configuration["DevWebServer:Path"];
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var directory = Path.GetFullPath(path, app.Environment.ContentRootPath);
        if (!Directory.Exists(directory))
        {
            app.Logger.LogWarning("Web folder {Directory} not found; start the frontend yourself", directory);
            return;
        }

        // npm is npm.cmd on Windows, which needs a shell to launch.
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", "npm", "run", "dev" } }
            : new ProcessStartInfo("npm") { ArgumentList = { "run", "dev" } };
        startInfo.WorkingDirectory = directory;
        startInfo.UseShellExecute = false;

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            app.Logger.LogWarning("Could not start the frontend ({Error}); run npm run dev in {Directory}", exception.Message, directory);
            return;
        }

        if (process is null)
        {
            return;
        }

        app.Logger.LogInformation("Frontend dev server started from {Directory}", directory);
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }
            process.Dispose();
        });
    }
}
