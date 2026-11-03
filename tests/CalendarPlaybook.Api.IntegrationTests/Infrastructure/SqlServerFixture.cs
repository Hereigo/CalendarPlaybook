using CalendarPlaybook.Api.Infrastructure;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(CalendarPlaybook.Api.IntegrationTests.Infrastructure.SqlServerFixture))]

namespace CalendarPlaybook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One SQL Server 2022 Express container per test run, with the EF migrations applied, and the API factory on top.
/// When no Podman/Docker endpoint is reachable, the integration tests are skipped instead of failing,
/// unless <c>CALENDAR_INTEGRATION_REQUIRED=true</c> (CI, spec section 12).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same image as compose.yaml, pinned by digest.
    private const string Image =
        "mcr.microsoft.com/mssql/server:2022-latest@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718";

    // Podman machine's API pipe on Windows. Used when DOCKER_HOST is not set and the pipe exists.
    private const string PodmanPipe = "podman-machine-default";

    private MsSqlContainer? container;
    private CalendarApiFactory? factory;
    private string? skipReason;

    public CalendarApiFactory Factory
    {
        get
        {
            if (skipReason is not null)
            {
                Assert.Skip(skipReason);
            }

            return factory!;
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            var builder = new MsSqlBuilder(Image).WithEnvironment("MSSQL_PID", "Express");
            if (PodmanPipeEndpoint() is { } endpoint)
            {
                builder = builder.WithDockerEndpoint(endpoint);
            }

            container = builder.Build();
            await container.StartAsync();
        }
        catch (DockerUnavailableException exception) when (!IsRequired())
        {
            skipReason = "Podman/Docker is not reachable, so the integration tests were skipped. " +
                "Start the Podman machine (see docs/local-setup.md, Tests). Detail: " + exception.Message;
            return;
        }

        factory = new CalendarApiFactory(container.GetConnectionString());

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CalendarDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    private static bool IsRequired()
    {
        return string.Equals(Environment.GetEnvironmentVariable("CALENDAR_INTEGRATION_REQUIRED"), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri? PodmanPipeEndpoint()
    {
        if (!OperatingSystem.IsWindows() || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return null;
        }

        var pipeExists = Directory.GetFiles(@"\\.\pipe\")
            .Any(pipe => string.Equals(Path.GetFileName(pipe), PodmanPipe, StringComparison.OrdinalIgnoreCase));

        return pipeExists ? new Uri($"npipe://./pipe/{PodmanPipe}") : null;
    }
}
