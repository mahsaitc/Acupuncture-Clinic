using System.Globalization;
using System.Security.Claims;
using Clinic.Application.Common;
using Clinic.Application.Scheduling;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Staff;

/// <summary>Day view of every appointment in the clinic, for admin, doctors and reception.</summary>
public class AppointmentsModel(ClinicDbContext db, IBookingService booking, ClinicTime clinicTime, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    public DateOnly Day { get; private set; }
    public List<Row> Rows { get; private set; } = [];

    public record Row(int Id, DateTime StartUtc, string Patient, string? Phone, string Doctor, ClinicService? Service, AppointmentStatus Status, string? Note);

    public static readonly AppointmentStatus[] SettableStatuses =
        [AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];

    public async Task OnGetAsync()
    {
        Day = DateOnly.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : clinicTime.Today(time.GetUtcNow().UtcDateTime);

        var fromUtc = clinicTime.ToUtc(Day, TimeOnly.MinValue);
        var toUtc = clinicTime.ToUtc(Day.AddDays(1), TimeOnly.MinValue);

        Rows = await (
                from a in db.Appointments.AsNoTracking().Include(a => a.Service)
                join p in db.Users on a.PatientUserId equals p.Id
                join doc in db.Doctors on a.DoctorProfileId equals doc.Id
                join du in db.Users on doc.UserId equals du.Id
                where a.StartUtc >= fromUtc && a.StartUtc < toUtc
                orderby a.StartUtc
                select new Row(a.Id, a.StartUtc, p.FullName, p.PhoneNumber, du.FullName, a.Service, a.Status, a.PatientNote))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, AppointmentStatus status)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is not null && SettableStatuses.Contains(status) && appointment.Status != AppointmentStatus.Cancelled)
        {
            appointment.Status = status;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Date });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        await booking.CancelAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!, isStaff: true);
        return RedirectToPage(new { Date });
    }
}
