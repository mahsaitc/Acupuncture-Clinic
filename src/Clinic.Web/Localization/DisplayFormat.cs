using System.Globalization;
using Clinic.Application.Common;

namespace Clinic.Web.Localization;

/// <summary>Formats UTC timestamps for the current language: Jalali for Persian, Gregorian for English.</summary>
public class DisplayFormat(ClinicTime clinicTime)
{
    public string Date(DateTime utc) => DateOnly(clinicTime.ToLocal(utc));

    public string DateOnly(DateTime local) => CulturePath.IsEnglish
        ? local.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture)
        : JalaliDate.ToLongString(local);

    /// <summary>e.g. 1405/07/14, or 6 Oct 2026 in English.</summary>
    public string ShortDate(DateTime utc)
    {
        var local = clinicTime.ToLocal(utc);
        return CulturePath.IsEnglish ? local.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : JalaliDate.ToShortString(local);
    }

    public string Day(DateOnly date) => DateOnly(date.ToDateTime(TimeOnly.MinValue));

    /// <summary>The day of the month in the page's calendar, e.g. ۱۴.</summary>
    public string DayOfMonth(DateOnly date) => CulturePath.IsEnglish
        ? date.Day.ToString(CultureInfo.InvariantCulture)
        : Number(JalaliDate.DayOfMonth(date.ToDateTime(TimeOnly.MinValue)));

    /// <summary>e.g. مهر ۱۴۰۵, or October 2026.</summary>
    public string MonthAndYear(DateOnly date) => CulturePath.IsEnglish
        ? date.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
        : JalaliDate.MonthAndYear(date.ToDateTime(TimeOnly.MinValue));

    public string DayName(DateOnly date) => CulturePath.IsEnglish
        ? date.ToString("ddd", CultureInfo.InvariantCulture)
        : JalaliDate.DayName(date.DayOfWeek);

    public string Time(DateTime utc) => Number(clinicTime.ToLocal(utc).ToString("HH:mm", CultureInfo.InvariantCulture));

    public string DateTime(DateTime utc) => $"{Date(utc)} - {Time(utc)}";

    public string Number(string text) => CulturePath.IsEnglish ? text : JalaliDate.ToPersianDigits(text);

    public string Number(int value) => Number(value.ToString(CultureInfo.InvariantCulture));

    public string Number(double value) => Number(value.ToString("0.#", CultureInfo.InvariantCulture));

    /// <summary>A date as typed into a form: 1405/07/14 in Persian, 2026-10-06 in English.</summary>
    public static string DateInput(DateOnly? date) => date is not DateOnly d ? ""
        : CulturePath.IsEnglish ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : JalaliDate.ToShortString(d.ToDateTime(TimeOnly.MinValue));

    public static string DateInputHint => CulturePath.IsEnglish ? "2026-10-06" : "۱۴۰۵/۰۷/۱۴";

    /// <summary>Reads a date typed in either calendar: Jalali when the year looks Jalali, otherwise Gregorian.</summary>
    public static bool TryParseDateInput(string? text, out DateOnly date)
    {
        if (JalaliDate.TryParse(text, out date))
        {
            return true;
        }
        return System.DateOnly.TryParseExact(JalaliDate.ToLatinDigits(text ?? "").Trim(), ["yyyy-MM-dd", "yyyy/MM/dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && date.Year > 1900;
    }
}
