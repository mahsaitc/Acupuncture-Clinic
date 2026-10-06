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

    /// <summary>e.g. 1405/07/14</summary>
    public static string ToShortString(DateTime date) => ToPersianDigits(
        $"{Calendar.GetYear(date):0000}/{Calendar.GetMonth(date):00}/{Calendar.GetDayOfMonth(date):00}");

    /// <summary>e.g. سه‌شنبه ۱۴ مهر ۱۴۰۵</summary>
    public static string ToLongString(DateTime date) => ToPersianDigits(
        $"{DayNames[date.DayOfWeek]} {Calendar.GetDayOfMonth(date)} {MonthNames[Calendar.GetMonth(date) - 1]} {Calendar.GetYear(date)}");

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
