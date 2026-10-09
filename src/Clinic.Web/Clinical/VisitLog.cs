using System.Globalization;
using Clinic.Application.Common;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Clinical;

/// <summary>
/// One visit to the clinic: an appointment that took place, a treatment session, or both when the session was held at
/// the appointment (same patient, doctor and day).
/// </summary>
public sealed record Visit(
    DateTime Utc,
    DateOnly Day,
    string PatientUserId,
    int? DoctorProfileId,
    string? DoctorUserId,
    int? AppointmentId,
    AppointmentStatus? Status,
    string? ServiceFa,
    string? ServiceEn,
    int? SessionId,
    SessionType? Treatment,
    int? PainScore)
{
    public string? Service(bool english) => english ? ServiceEn : ServiceFa;
}

/// <summary>The visits the signed-in staff member may see: a doctor sees their own; the admin and reception see all.</summary>
public sealed class VisitLog(ClinicDbContext db, StaffScope scope, ClinicTime clinicTime, TimeProvider time)
{
    private static readonly AppointmentStatus[] NotHeld = [AppointmentStatus.Cancelled, AppointmentStatus.NoShow];

    /// <summary>Visits on the clinic days from <paramref name="from"/> to <paramref name="to"/>, both included, oldest first.</summary>
    public async Task<List<Visit>> BetweenAsync(DateOnly from, DateOnly to, string? patientUserId = null, int? doctorId = null)
    {
        var fromUtc = clinicTime.ToUtc(from, TimeOnly.MinValue);
        var toUtc = clinicTime.ToUtc(to.AddDays(1), TimeOnly.MinValue);
        var nowUtc = time.GetUtcNow().UtcDateTime;

        var appointments = (await scope.AppointmentsAsync(db.Appointments)).AsNoTracking()
            .Where(a => a.StartUtc >= fromUtc && a.StartUtc < toUtc && a.StartUtc <= nowUtc && !NotHeld.Contains(a.Status));
        var sessions = scope.Sessions(db.TreatmentSessions).AsNoTracking().Where(s => s.DateUtc >= fromUtc && s.DateUtc < toUtc);
        if (patientUserId is not null)
        {
            appointments = appointments.Where(a => a.PatientUserId == patientUserId);
            sessions = sessions.Where(s => s.PatientUserId == patientUserId);
        }

        var doctors = await db.Doctors.AsNoTracking().Select(d => new { d.Id, d.UserId }).ToListAsync();
        var doctorByUser = doctors.GroupBy(d => d.UserId).ToDictionary(g => g.Key, g => g.First().Id);
        var userByDoctor = doctors.ToDictionary(d => d.Id, d => d.UserId);
        if (doctorId is int only)
        {
            var onlyUser = userByDoctor.GetValueOrDefault(only);
            appointments = appointments.Where(a => a.DoctorProfileId == only);
            sessions = sessions.Where(s => s.DoctorUserId == onlyUser);
        }

        var booked = await appointments
            .Select(a => new { a.Id, a.StartUtc, a.PatientUserId, a.DoctorProfileId, a.Status, a.Service!.NameFa, a.Service.NameEn })
            .ToListAsync();
        var held = await sessions
            .Select(s => new { s.Id, s.DateUtc, s.PatientUserId, s.DoctorUserId, s.Type, s.PainScore })
            .ToListAsync();

        var visits = new List<Visit>();
        var used = new HashSet<int>();
        foreach (var s in held)
        {
            var day = Day(s.DateUtc);
            var doctor = doctorByUser.TryGetValue(s.DoctorUserId, out var d) ? d : (int?)null;
            var match = booked.FirstOrDefault(a => !used.Contains(a.Id) && a.PatientUserId == s.PatientUserId
                && a.DoctorProfileId == doctor && Day(a.StartUtc) == day);
            if (match is not null)
            {
                used.Add(match.Id);
            }
            visits.Add(new Visit(match?.StartUtc ?? s.DateUtc, day, s.PatientUserId, doctor, s.DoctorUserId, match?.Id, match?.Status,
                match?.NameFa, match?.NameEn, s.Id, s.Type, s.PainScore));
        }
        foreach (var a in booked.Where(a => !used.Contains(a.Id)))
        {
            visits.Add(new Visit(a.StartUtc, Day(a.StartUtc), a.PatientUserId, a.DoctorProfileId, userByDoctor.GetValueOrDefault(a.DoctorProfileId),
                a.Id, a.Status, a.NameFa, a.NameEn, null, null, null));
        }
        return visits.OrderBy(v => v.Utc).ToList();
    }

    public DateOnly Day(DateTime utc) => DateOnly.FromDateTime(clinicTime.ToLocal(utc));

    public DateOnly Today => clinicTime.Today(time.GetUtcNow().UtcDateTime);
}

public enum PeriodKind
{
    Day,
    Week,
    Month,
    Year,
}

/// <summary>Clinic periods in the Persian calendar: weeks start on Saturday, months and years are Jalali.</summary>
public static class Periods
{
    private static readonly PersianCalendar Calendar = new();

    private static readonly string[] MonthsFa = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];
    private static readonly string[] MonthsEn = ["Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar", "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand"];

    public static DateOnly Start(DateOnly day, PeriodKind kind)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        return kind switch
        {
            PeriodKind.Week => day.AddDays(-(((int)day.DayOfWeek + 1) % 7)),
            PeriodKind.Month => DateOnly.FromDateTime(Calendar.ToDateTime(Calendar.GetYear(date), Calendar.GetMonth(date), 1, 0, 0, 0, 0)),
            PeriodKind.Year => DateOnly.FromDateTime(Calendar.ToDateTime(Calendar.GetYear(date), 1, 1, 0, 0, 0, 0)),
            _ => day,
        };
    }

    public static DateOnly Next(DateOnly start, PeriodKind kind)
    {
        var date = start.ToDateTime(TimeOnly.MinValue);
        return kind switch
        {
            PeriodKind.Week => start.AddDays(7),
            PeriodKind.Month => DateOnly.FromDateTime(Calendar.AddMonths(date, 1)),
            PeriodKind.Year => DateOnly.FromDateTime(Calendar.AddYears(date, 1)),
            _ => start.AddDays(1),
        };
    }

    /// <summary>The period containing <paramref name="day"/>: its first and last day.</summary>
    public static (DateOnly From, DateOnly To) Containing(DateOnly day, PeriodKind kind)
    {
        var start = Start(day, kind);
        return (start, Next(start, kind).AddDays(-1));
    }

    /// <summary>The last <paramref name="count"/> periods up to the one containing <paramref name="day"/>, oldest first.</summary>
    public static List<DateOnly> Last(DateOnly day, PeriodKind kind, int count)
    {
        var starts = new List<DateOnly> { Start(day, kind) };
        while (starts.Count < count)
        {
            starts.Insert(0, Start(starts[0].AddDays(-1), kind));
        }
        return starts;
    }

    /// <summary>A short label: "مهر ۱۴۰۵", "۱۴۰۵", or the first day of a week.</summary>
    public static string Label(DateOnly start, PeriodKind kind, bool english)
    {
        var date = start.ToDateTime(TimeOnly.MinValue);
        var y = Calendar.GetYear(date);
        var m = Calendar.GetMonth(date);
        var d = Calendar.GetDayOfMonth(date);
        var text = kind switch
        {
            PeriodKind.Year => y.ToString(CultureInfo.InvariantCulture),
            PeriodKind.Month => english ? $"{MonthsEn[m - 1]} {y}" : $"{MonthsFa[m - 1]} {y}",
            _ => $"{y}/{m:00}/{d:00}",
        };
        return english ? text : JalaliDate.ToPersianDigits(text);
    }
}
