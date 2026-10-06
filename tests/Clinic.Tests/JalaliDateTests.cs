using Clinic.Application.Common;

namespace Clinic.Tests;

public class JalaliDateTests
{
    [Fact]
    public void Formats_short_date_with_persian_digits() =>
        Assert.Equal("۱۴۰۵/۰۷/۱۴", JalaliDate.ToShortString(new DateTime(2026, 10, 6)));

    [Fact]
    public void Formats_long_date_with_weekday_and_month_name() =>
        Assert.Equal("سه‌شنبه ۱۴ مهر ۱۴۰۵", JalaliDate.ToLongString(new DateTime(2026, 10, 6)));

    [Fact]
    public void Nowruz_is_the_first_of_farvardin() =>
        Assert.Equal("۱۴۰۵/۰۱/۰۱", JalaliDate.ToShortString(new DateTime(2026, 3, 21)));
}
