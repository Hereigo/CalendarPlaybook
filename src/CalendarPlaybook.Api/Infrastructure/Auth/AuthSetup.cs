using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CalendarPlaybook.Api.Infrastructure.Auth;

public static class AuthSetup
{
    /// <summary>
    /// Validates cidaas access tokens (issuer, audience, signature, expiry; spec section 3).
    /// <c>Cidaas:BaseUrl</c> (the tenant URL, e.g. <c>https://your-tenant.cidaas.eu</c>) and <c>Cidaas:Audience</c>
    /// (the token <c>aud</c>: the SPA client id by default, or an API resource URI) come from configuration
    /// (environment or user secrets). The signing keys and issuer are read from the tenant's OpenID discovery document.
    /// The app refuses to start when either is missing, instead of rejecting every request with <c>401</c>.
    /// </summary>
    public static IServiceCollection AddCidaas(this IServiceCollection services, IConfiguration configuration)
    {
        // Checked on host start rather than here, so design-time tools (dotnet ef) still work without cidaas config.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Validate(
                _ => !string.IsNullOrWhiteSpace(configuration["Cidaas:BaseUrl"])
                    && !string.IsNullOrWhiteSpace(configuration["Cidaas:Audience"]),
                "Cidaas:BaseUrl and Cidaas:Audience must be set. Set them with " +
                "`dotnet user-secrets set Cidaas:BaseUrl \"https://<tenant>.cidaas.eu\" --project src/CalendarPlaybook.Api` " +
                "(and Cidaas:Audience likewise) or the Cidaas__BaseUrl and Cidaas__Audience environment variables.")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Cidaas:BaseUrl"]?.TrimEnd('/');
                options.Audience = configuration["Cidaas:Audience"];

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
