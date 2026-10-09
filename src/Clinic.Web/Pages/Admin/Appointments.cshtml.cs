using System.Globalization;
using System.Security.Claims;
using Clinic.Application.Common;
using Clinic.Application.Scheduling;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

/// <summary>Day, week and month views of every appointment in the clinic, for admin, doctors and reception.</summary>
public class AppointmentsModel(ClinicDbContext db, IBookingService booking, ClinicTime clinicTime, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    /// <summary>day, week or month.</summary>
    [BindProperty(SupportsGet = true)]
    public string? View { get; set; }

    public CalendarView Mode { get; private set; }
    public DateOnly Day { get; private set; }

    /// <summary>The days shown: one, a week from Saturday, or whole weeks covering the month.</summary>
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }

    /// <summary>The first and last day of the month in month view (Jalali in Persian).</summary>
    public DateOnly MonthStart { get; private set; }
    public DateOnly MonthEnd { get; private set; }

    public List<Row> Rows { get; private set; } = [];
    public ILookup<DateOnly, Row> ByDay { get; private set; } = Enumerable.Empty<Row>().ToLookup(r => default(DateOnly));

    public enum CalendarView { Day, Week, Month }

    public record Row(int Id, DateTime StartUtc, string Patient, string? Phone, string Doctor, ClinicService? Service, AppointmentStatus Status, string? Note);

    public static readonly AppointmentStatus[] SettableStatuses =
        [AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];

    public async Task OnGetAsync()
    {
        Day = DateOnly.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : clinicTime.Today(time.GetUtcNow().UtcDateTime);

        Mode = Enum.TryParse<CalendarView>(View, ignoreCase: true, out var mode) ? mode : CalendarView.Day;

        switch (Mode)
        {
            case CalendarView.Week:
                From = WeekStart(Day);
                To = From.AddDays(7);
                break;
            case CalendarView.Month:
                (MonthStart, MonthEnd) = MonthOf(Day, Clinic.Web.Localization.CulturePath.IsEnglish);
                From = WeekStart(MonthStart);
                To = WeekStart(MonthEnd).AddDays(7);
                break;
            default:
                From = Day;
                To = Day.AddDays(1);
                break;
        }

        var fromUtc = clinicTime.ToUtc(From, TimeOnly.MinValue);
        var toUtc = clinicTime.ToUtc(To, TimeOnly.MinValue);

        Rows = await (
                from a in db.Appointments.AsNoTracking().Include(a => a.Service)
                join p in db.Users on a.PatientUserId equals p.Id
                join doc in db.Doctors on a.DoctorProfileId equals doc.Id
                join du in db.Users on doc.UserId equals du.Id
                where a.StartUtc >= fromUtc && a.StartUtc < toUtc
                orderby a.StartUtc
                select new Row(a.Id, a.StartUtc, p.FullName, p.PhoneNumber, du.FullName, a.Service, a.Status, a.PatientNote))
            .ToListAsync();
        ByDay = Rows.ToLookup(r => DateOnly.FromDateTime(clinicTime.ToLocal(r.StartUtc)));
    }

    /// <summary>Weeks start on Saturday, as in Iran.</summary>
    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 1) % 7));

    /// <summary>The month that contains the day: Jalali for Persian pages, Gregorian for English ones.</summary>
    public static (DateOnly First, DateOnly Last) MonthOf(DateOnly day, bool gregorian)
    {
        if (gregorian)
        {
            var first = new DateOnly(day.Year, day.Month, 1);
            return (first, first.AddMonths(1).AddDays(-1));
        }
        var pc = new PersianCalendar();
        var dt = day.ToDateTime(TimeOnly.MinValue);
        var start = DateOnly.FromDateTime(pc.ToDateTime(pc.GetYear(dt), pc.GetMonth(dt), 1, 0, 0, 0, 0));
        return (start, start.AddDays(pc.GetDaysInMonth(pc.GetYear(dt), pc.GetMonth(dt)) - 1));
    }

    /// <summary>The same day in the previous or next day, week or month.</summary>
    public DateOnly Step(int direction) => Mode switch
    {
        CalendarView.Week => Day.AddDays(7 * direction),
        CalendarView.Month => direction > 0 ? MonthEnd.AddDays(1) : MonthStart.AddDays(-1),
        _ => Day.AddDays(direction),
    };

    public async Task<IActionResult> OnPostStatusAsync(int id, AppointmentStatus status)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is not null && SettableStatuses.Contains(status) && appointment.Status != AppointmentStatus.Cancelled)
        {
            appointment.Status = status;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Date, View });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        await booking.CancelAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!, isStaff: true);
        return RedirectToPage(new { Date, View });
    }
}
