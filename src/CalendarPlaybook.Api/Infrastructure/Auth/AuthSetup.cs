using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CalendarPlaybook.Api.Infrastructure.Auth;

public static class AuthSetup
{
    /// <summary>
    /// Validates Auth0 access tokens (issuer, audience, signature, expiry; spec section 3).
    /// <c>Auth0:Domain</c> and <c>Auth0:Audience</c> come from configuration (environment or user secrets).
    /// The app refuses to start when either is missing, instead of rejecting every request with <c>401</c>.
    /// </summary>
    public static IServiceCollection AddAuth0(this IServiceCollection services, IConfiguration configuration)
    {
        // Checked on host start rather than here, so design-time tools (dotnet ef) still work without Auth0 config.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Validate(
                _ => !string.IsNullOrWhiteSpace(configuration["Auth0:Domain"])
                    && !string.IsNullOrWhiteSpace(configuration["Auth0:Audience"]),
                "Auth0:Domain and Auth0:Audience must be set. Set them with " +
                "`dotnet user-secrets set Auth0:Domain \"<tenant>.auth0.com\" --project src/CalendarPlaybook.Api` " +
                "(and Auth0:Audience likewise) or the Auth0__Domain and Auth0__Audience environment variables.")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{configuration["Auth0:Domain"]}/";
                options.Audience = configuration["Auth0:Audience"];

                // Keep "sub" as "sub" instead of mapping it to the long NameIdentifier claim type.
                options.MapInboundClaims = false;

                // A token without a usable sub has no owner, so it is an invalid token: 401, not a 500 later in GetOwnerId().
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Principal?.FindFirstValue("sub")))
                        {
                            context.Fail("The token has no sub claim.");
                        }

                        return Task.CompletedTask;
                    }
                };
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
