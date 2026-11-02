using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalendarPlaybook.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartLocalTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndLocalTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Interval = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    Month = table.Column<int>(type: "int", nullable: true),
                    DayOfWeek = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    UntilDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Events_DayOfWeek", "[DayOfWeek] IS NULL OR [DayOfWeek] IN ('Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')");
                    table.CheckConstraint("CK_Events_Kind", "([Kind] = 'Single' AND [StartUtc] IS NOT NULL AND [EndUtc] IS NOT NULL AND [StartDate] IS NULL AND [EndDate] IS NULL AND [StartLocalTime] IS NULL AND [EndLocalTime] IS NULL AND [RuleType] IS NULL AND [Interval] IS NULL AND [DayOfMonth] IS NULL AND [Month] IS NULL AND [DayOfWeek] IS NULL AND [UntilDate] IS NULL) OR ([Kind] = 'AllDay' AND [StartDate] IS NOT NULL AND [EndDate] IS NOT NULL AND [StartUtc] IS NULL AND [EndUtc] IS NULL AND [StartLocalTime] IS NULL AND [EndLocalTime] IS NULL AND [RuleType] IS NULL AND [Interval] IS NULL AND [DayOfMonth] IS NULL AND [Month] IS NULL AND [DayOfWeek] IS NULL AND [UntilDate] IS NULL) OR ([Kind] = 'Recurring' AND [StartDate] IS NOT NULL AND [RuleType] IS NOT NULL AND [UntilDate] IS NOT NULL AND [StartUtc] IS NULL AND [EndUtc] IS NULL AND [EndDate] IS NULL AND (([StartLocalTime] IS NULL AND [EndLocalTime] IS NULL) OR ([StartLocalTime] IS NOT NULL AND [EndLocalTime] IS NOT NULL)))");
                    table.CheckConstraint("CK_Events_RuleType", "[RuleType] IS NULL OR ([RuleType] = 'EveryDays' AND [Interval] IS NOT NULL AND [DayOfMonth] IS NULL AND [Month] IS NULL AND [DayOfWeek] IS NULL) OR ([RuleType] = 'MonthDay' AND [DayOfMonth] IS NOT NULL AND [Interval] IS NULL AND [Month] IS NULL AND [DayOfWeek] IS NULL) OR ([RuleType] = 'WeekDay' AND [DayOfWeek] IS NOT NULL AND [Interval] IS NULL AND [DayOfMonth] IS NULL AND [Month] IS NULL) OR ([RuleType] = 'YearDay' AND [Month] IS NOT NULL AND [DayOfMonth] IS NOT NULL AND [Interval] IS NULL AND [DayOfWeek] IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_OwnerId_StartDate",
                table: "Events",
                columns: new[] { "OwnerId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_OwnerId_StartUtc",
                table: "Events",
                columns: new[] { "OwnerId", "StartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
