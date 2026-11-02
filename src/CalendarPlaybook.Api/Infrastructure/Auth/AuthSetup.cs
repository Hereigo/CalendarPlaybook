using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CalendarPlaybook.Api.Infrastructure.Auth;

public static class AuthSetup
{
    /// <summary>
    /// Validates Auth0 access tokens (issuer, audience, signature, expiry; spec section 3).
    /// <c>Auth0:Domain</c> and <c>Auth0:Audience</c> come from configuration (environment or user secrets).
    /// </summary>
    public static IServiceCollection AddAuth0(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{configuration["Auth0:Domain"]}/";
                options.Audience = configuration["Auth0:Audience"];

                // Keep "sub" as "sub" instead of mapping it to the long NameIdentifier claim type.
                options.MapInboundClaims = false;
            });

        return services.AddAuthorization();
    }

    /// <summary>The owner id of every event: the token <c>sub</c> claim.</summary>
    public static string GetOwnerId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("The authenticated user has no sub claim.");
    }
}
