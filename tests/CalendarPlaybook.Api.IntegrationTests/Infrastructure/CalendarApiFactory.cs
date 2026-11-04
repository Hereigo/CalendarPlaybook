using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarPlaybook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API pipeline on a test server, against the test SQL Server and the local JWKS.
/// Environment <c>Testing</c> keeps user secrets, the dev SQL container and startup migrations out of the run.
/// </summary>
public sealed class CalendarApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Calendar", connectionString);
        builder.UseSetting("Cidaas:BaseUrl", TestTokens.BaseUrl);
        builder.UseSetting("Cidaas:Audience", TestTokens.Audience);

        // A static configuration replaces the metadata download from cidaas; validation itself is unchanged.
        builder.ConfigureTestServices(services =>
            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options => options.Configuration = TestTokens.OpenIdConfiguration));
    }
}
