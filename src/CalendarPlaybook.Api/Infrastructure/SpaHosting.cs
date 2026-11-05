namespace CalendarPlaybook.Api.Infrastructure;

/// <summary>
/// Hosting (non-Development): serves the React build that <c>dotnet publish</c> copies to <c>wwwroot</c>,
/// so the API and the UI share one origin. Does nothing when <c>wwwroot/index.html</c> is missing.
/// </summary>
public static class SpaHosting
{
    public static void MapSpa(this WebApplication app)
    {
        if (!app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists)
        {
            app.Logger.LogWarning("wwwroot/index.html not found; the UI is not served. Publish with dotnet publish to include it");
            return;
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Unknown API routes stay 404 instead of returning the SPA page.
        app.MapFallback("/api/{**path}", () => Results.NotFound());
        // Client-side routes (paths without a file extension) get the SPA page.
        app.MapFallbackToFile("index.html");
    }
}
