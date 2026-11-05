using CalendarPlaybook.Api.Infrastructure.LegacyImport;

namespace CalendarPlaybook.Api.Tests.LegacyImport;

public class LegacyDataSeederTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(150, false)]
    public void ShouldSeed_OnlyWhenTableIsEmptyOrHasOneRow(int existingRows, bool expected)
    {
        Assert.Equal(expected, LegacyDataSeeder.ShouldSeed(existingRows));
    }

    [Fact]
    public void ResolvePath_RelativePath_ResolvesAgainstAppFolder()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "app");

        var resolved = LegacyDataSeeder.ResolvePath("SeedData/database_backup.json", baseDir);

        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "SeedData", "database_backup.json")), resolved);
    }

    [Fact]
    public void ResolvePath_AbsolutePath_IsKept()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "backup.json");

        Assert.Equal(absolute, LegacyDataSeeder.ResolvePath(absolute, "ignored"));
    }
}
