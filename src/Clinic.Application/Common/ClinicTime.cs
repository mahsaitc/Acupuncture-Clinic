namespace Clinic.Application.Common;

/// <summary>
/// Converts between UTC (what the database stores) and the clinic's local time (what people see).
/// </summary>
public sealed class ClinicTime
{
    public ClinicTime(string timeZoneId)
    {
        Zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public TimeZoneInfo Zone { get; }

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), Zone);

    public DateOnly Today(DateTime nowUtc) => DateOnly.FromDateTime(ToLocal(nowUtc));
}
