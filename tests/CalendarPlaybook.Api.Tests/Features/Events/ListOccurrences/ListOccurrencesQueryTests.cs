using CalendarPlaybook.Api.Features.Events.ListOccurrences;

namespace CalendarPlaybook.Api.Tests.Features.Events.ListOccurrences;

public class ListOccurrencesQueryTests
{
    [Fact]
    public void ValidRange_ParsesBothDates()
    {
        var (query, errors) = ListOccurrencesQuery.Parse("2027-06-01", "2027-07-12");

        Assert.Empty(errors);
        Assert.Equal(new ListOccurrencesQuery(new DateOnly(2027, 6, 1), new DateOnly(2027, 7, 12)), query);
    }

    [Fact]
    public void SingleDayRange_IsValid()
    {
        var (query, errors) = ListOccurrencesQuery.Parse("2027-06-01", "2027-06-01");

        Assert.Empty(errors);
        Assert.NotNull(query);
    }

    [Fact]
    public void Range_Of370DaysInclusive_IsValid_And371IsNot()
    {
        // 2027-01-01 plus 369 days is 2028-01-05: 370 days counting both ends.
        Assert.Empty(ListOccurrencesQuery.Parse("2027-01-01", "2028-01-05").Errors);

        var (query, errors) = ListOccurrencesQuery.Parse("2027-01-01", "2028-01-06");

        Assert.Null(query);
        Assert.Contains("to", errors.Keys);
    }

    [Theory]
    [InlineData(null, "2027-06-01", "from")]
    [InlineData("", "2027-06-01", "from")]
    [InlineData("2027-06-01", null, "to")]
    [InlineData("2027-06-01", " ", "to")]
    public void MissingValue_IsReportedByParameterName(string? from, string? to, string field)
    {
        var (query, errors) = ListOccurrencesQuery.Parse(from, to);

        Assert.Null(query);
        Assert.Contains(field, errors.Keys);
    }

    [Theory]
    [InlineData("01.06.2027")]
    [InlineData("2027-6-1")]
    [InlineData("2027-02-30")]
    [InlineData("2027-06-01T10:00:00")]
    [InlineData("tomorrow")]
    public void BadFormat_IsReported(string value)
    {
        var (query, errors) = ListOccurrencesQuery.Parse(value, "2027-07-01");

        Assert.Null(query);
        Assert.Contains("from", errors.Keys);
    }

    [Fact]
    public void ToBeforeFrom_IsReported()
    {
        var (query, errors) = ListOccurrencesQuery.Parse("2027-06-02", "2027-06-01");

        Assert.Null(query);
        Assert.Contains("to", errors.Keys);
    }

    [Theory]
    [InlineData("0001-01-01", "0001-01-05")]
    [InlineData("9999-12-30", "9999-12-31")]
    public void DatesAtTheEdgesOfDateTime_AreRejectedInsteadOfOverflowing(string from, string to)
    {
        var (query, errors) = ListOccurrencesQuery.Parse(from, to);

        Assert.Null(query);
        Assert.NotEmpty(errors);
    }
}
