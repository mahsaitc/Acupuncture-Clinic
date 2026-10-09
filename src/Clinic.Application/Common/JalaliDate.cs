using System.Globalization;

namespace Clinic.Application.Common;

/// <summary>Formats dates in the Persian (Jalali / Shamsi) calendar with Persian digits.</summary>
public static class JalaliDate
{
    private static readonly PersianCalendar Calendar = new();

    private static readonly string[] MonthNames =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
    ];

    private static readonly Dictionary<DayOfWeek, string> DayNames = new()
    {
        [DayOfWeek.Saturday] = "شنبه",
        [DayOfWeek.Sunday] = "یکشنبه",
        [DayOfWeek.Monday] = "دوشنبه",
        [DayOfWeek.Tuesday] = "سه‌شنبه",
        [DayOfWeek.Wednesday] = "چهارشنبه",
        [DayOfWeek.Thursday] = "پنجشنبه",
        [DayOfWeek.Friday] = "جمعه",
    };

    public static int Year(DateTime date) => Calendar.GetYear(date);

    public static int DayOfMonth(DateTime date) => Calendar.GetDayOfMonth(date);

    public static string DayName(DayOfWeek day) => DayNames[day];

    /// <summary>e.g. مهر ۱۴۰۵</summary>
    public static string MonthAndYear(DateTime date) =>
        ToPersianDigits($"{MonthNames[Calendar.GetMonth(date) - 1]} {Calendar.GetYear(date)}");

    /// <summary>e.g. 1405/07/14</summary>
    public static string ToShortString(DateTime date) => ToPersianDigits(
        $"{Calendar.GetYear(date):0000}/{Calendar.GetMonth(date):00}/{Calendar.GetDayOfMonth(date):00}");

    /// <summary>e.g. سه‌شنبه ۱۴ مهر ۱۴۰۵</summary>
    public static string ToLongString(DateTime date) => ToPersianDigits(
        $"{DayNames[date.DayOfWeek]} {Calendar.GetDayOfMonth(date)} {MonthNames[Calendar.GetMonth(date) - 1]} {Calendar.GetYear(date)}");

    /// <summary>Parses 1405/07/14 (Persian or Latin digits, / or - between parts).</summary>
    public static bool TryParse(string? text, out DateOnly date)
    {
        date = default;
        var parts = ToLatinDigits(text ?? "").Trim().Split('/', '-');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var d)
            || y < 1300 || y > 1500 || m < 1 || m > 12 || d < 1 || d > Calendar.GetDaysInMonth(y, m))
        {
            return false;
        }
        date = DateOnly.FromDateTime(Calendar.ToDateTime(y, m, d, 0, 0, 0, 0));
        return true;
    }

    public static string ToLatinDigits(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '۰' and <= '۹')
            {
                chars[i] = (char)('0' + (chars[i] - '۰'));
            }
            else if (chars[i] is >= '٠' and <= '٩')
            {
                chars[i] = (char)('0' + (chars[i] - '٠'));
            }
        }
        return new string(chars);
    }

    public static string ToPersianDigits(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
            {
                chars[i] = (char)('۰' + (chars[i] - '0'));
            }
        }
        return new string(chars);
    }
}
