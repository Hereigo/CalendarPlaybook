using Microsoft.AspNetCore.StaticFiles;

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

        // index.html names the current hashed bundles, so browsers must revalidate it on every load;
        // otherwise a cached copy (common on mobile) keeps loading an old bundle after a deploy.
        // The hashed files in assets/ never change and may be cached for a long time.
        var staticFiles = new StaticFileOptions { OnPrepareResponse = context => SetCacheHeaders(context) };

        app.UseDefaultFiles();
        app.UseStaticFiles(staticFiles);

        // Unknown API routes stay 404 instead of returning the SPA page.
        app.MapFallback("/api/{**path}", () => Results.NotFound());
        // Client-side routes (paths without a file extension) get the SPA page.
        app.MapFallbackToFile("index.html", staticFiles);
    }

    private static void SetCacheHeaders(StaticFileResponseContext context)
    {
        var headers = context.Context.Response.Headers;
        if (context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = "no-cache";
        }
        else if (context.Context.Request.Path.StartsWithSegments("/assets"))
        {
            headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
}
