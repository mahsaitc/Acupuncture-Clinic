namespace Clinic.Application.Scheduling;

public readonly record struct TimeRange(TimeOnly Start, TimeOnly End);

public readonly record struct BusyRange(DateTime StartUtc, DateTime EndUtc);

/// <summary>Pure logic that turns working hours and existing bookings into free appointment slots.</summary>
public static class SlotCalculator
{
    /// <param name="workingHours">The doctor's working blocks for <paramref name="date"/>'s weekday.</param>
    /// <param name="toUtc">Converts a local date and time on <paramref name="date"/> to UTC.</param>
    /// <returns>Free slot start times in UTC, ascending.</returns>
    public static IReadOnlyList<DateTime> FreeSlots(
        DateOnly date,
        IEnumerable<TimeRange> workingHours,
        int durationMinutes,
        IEnumerable<BusyRange> busy,
        Func<DateOnly, TimeOnly, DateTime> toUtc,
        DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMinutes);

        var duration = TimeSpan.FromMinutes(durationMinutes);
        var busyList = busy.ToList();
        var slots = new List<DateTime>();

        foreach (var block in workingHours.OrderBy(b => b.Start))
        {
            var blockStartUtc = toUtc(date, block.Start);
            var blockEndUtc = toUtc(date, block.End);

            for (var start = blockStartUtc; start + duration <= blockEndUtc; start += duration)
            {
                var end = start + duration;
                if (start <= nowUtc)
                {
                    continue;
                }
                if (busyList.Any(b => b.StartUtc < end && start < b.EndUtc))
                {
                    continue;
                }
                slots.Add(start);
            }
        }

        return slots;
    }
}
