using Clinic.Application.Common;
using Clinic.Application.Scheduling;

namespace Clinic.Tests;

public class SlotCalculatorTests
{
    private static readonly ClinicTime Tehran = new("Asia/Tehran");
    private static readonly DateOnly Day = new(2026, 10, 10);
    private static readonly DateTime LongAgo = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Splits_working_hours_into_slots_of_the_service_duration()
    {
        var slots = SlotCalculator.FreeSlots(Day, [new TimeRange(new(9, 0), new(11, 0))], 45, [], Tehran.ToUtc, LongAgo);

        // 09:00 and 09:45 fit; 10:30 + 45 min would end after 11:00.
        Assert.Equal([Tehran.ToUtc(Day, new(9, 0)), Tehran.ToUtc(Day, new(9, 45))], slots);
    }

    [Fact]
    public void Tehran_local_time_is_converted_to_utc()
    {
        var slot = SlotCalculator.FreeSlots(Day, [new TimeRange(new(9, 0), new(9, 30))], 30, [], Tehran.ToUtc, LongAgo).Single();

        Assert.Equal(new DateTime(2026, 10, 10, 5, 30, 0, DateTimeKind.Utc), slot);
    }

    [Fact]
    public void Skips_slots_that_overlap_a_booking()
    {
        var busy = new BusyRange(Tehran.ToUtc(Day, new(9, 15)), Tehran.ToUtc(Day, new(9, 45)));

        var slots = SlotCalculator.FreeSlots(Day, [new TimeRange(new(9, 0), new(10, 30))], 30, [busy], Tehran.ToUtc, LongAgo);

        Assert.Equal([Tehran.ToUtc(Day, new(10, 0))], slots);
    }

    [Fact]
    public void Skips_slots_in_the_past()
    {
        var now = Tehran.ToUtc(Day, new(9, 10));

        var slots = SlotCalculator.FreeSlots(Day, [new TimeRange(new(9, 0), new(10, 0))], 30, [], Tehran.ToUtc, now);

        Assert.Equal([Tehran.ToUtc(Day, new(9, 30))], slots);
    }

    [Fact]
    public void Rejects_a_non_positive_duration() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlotCalculator.FreeSlots(Day, [], 0, [], Tehran.ToUtc, LongAgo));
}
