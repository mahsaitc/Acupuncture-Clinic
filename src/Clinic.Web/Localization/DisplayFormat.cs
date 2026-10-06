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

    public string Day(DateOnly date) => DateOnly(date.ToDateTime(TimeOnly.MinValue));

    public string Time(DateTime utc) => Number(clinicTime.ToLocal(utc).ToString("HH:mm", CultureInfo.InvariantCulture));

    public string DateTime(DateTime utc) => $"{Date(utc)} - {Time(utc)}";

    public string Number(string text) => CulturePath.IsEnglish ? text : JalaliDate.ToPersianDigits(text);

    public string Number(int value) => Number(value.ToString(CultureInfo.InvariantCulture));
}
