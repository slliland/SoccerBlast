namespace SoccerBlast.Api.Services;

public static class DateRangeService
{
    // Converts a local date (e.g. 2026-02-12 in America/New_York)
    // into the UTC time range [startUtc, endUtc) that covers that whole local day.
    public static (DateTime startUtc, DateTime endUtc) GetUtcRangeForLocalDate(DateTime localDate, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // localDate should be Date-only (00:00)
        var localStart = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(localDate.Date.AddDays(1), DateTimeKind.Unspecified);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

        return (startUtc, endUtc);
    }
}
