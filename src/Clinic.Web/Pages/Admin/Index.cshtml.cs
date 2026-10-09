using Clinic.Application.Common;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

/// <summary>The panel's home. A doctor sees the figures of their own work; the admin and reception see the whole clinic.</summary>
public class IndexModel(ClinicDbContext db, Clinic.Web.Clinical.StaffScope scope, ClinicTime clinicTime, TimeProvider time) : PageModel
{
    public bool OwnOnly => scope.IsOwnOnly;
    public string? DoctorName { get; private set; }

    /// <summary>The doctor's own weekly hours, shown read-only; only the admin changes them.</summary>
    public List<WorkingHour> Hours { get; private set; } = [];

    public int TodayCount { get; private set; }
    public int WeekCount { get; private set; }
    public int PatientCount { get; private set; }
    public int UnreadMessages { get; private set; }
    public int PendingDoctors { get; private set; }
    public int PublishedPosts { get; private set; }

    public List<TodayRow> Today { get; private set; } = [];
    public List<ContactMessage> LatestMessages { get; private set; } = [];

    public record TodayRow(DateTime StartUtc, string Patient, ClinicService? Service, AppointmentStatus Status);

    public async Task OnGetAsync()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var today = clinicTime.Today(now);
        var fromUtc = clinicTime.ToUtc(today, TimeOnly.MinValue);
        var toUtc = clinicTime.ToUtc(today.AddDays(1), TimeOnly.MinValue);
        var weekUtc = clinicTime.ToUtc(today.AddDays(7), TimeOnly.MinValue);

        var active = (await scope.AppointmentsAsync(db.Appointments)).Where(a => a.Status != AppointmentStatus.Cancelled);
        TodayCount = await active.CountAsync(a => a.StartUtc >= fromUtc && a.StartUtc < toUtc);
        WeekCount = await active.CountAsync(a => a.StartUtc >= fromUtc && a.StartUtc < weekUtc);

        var patientRoleId = await db.Roles.Where(r => r.Name == Roles.Patient).Select(r => r.Id).FirstOrDefaultAsync();
        var patients = await scope.PatientsAsync(
            from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId where ur.RoleId == patientRoleId select u);
        PatientCount = await patients.CountAsync();
        var messages = await scope.MessagesAsync(db.ContactMessages);
        UnreadMessages = await messages.CountAsync(m => m.ReadUtc == null && !m.IsArchived);
        PendingDoctors = await db.Doctors.CountAsync(d => !d.IsApproved && db.Users.Any(u => u.Id == d.UserId && u.IsActive));
        PublishedPosts = await db.Posts.CountAsync(p => p.IsPublished);

        Today = await (
                from a in active.AsNoTracking().Include(a => a.Service)
                join p in db.Users on a.PatientUserId equals p.Id
                where a.StartUtc >= fromUtc && a.StartUtc < toUtc
                orderby a.StartUtc
                select new TodayRow(a.StartUtc, p.FullName, a.Service, a.Status))
            .ToListAsync();

        if (await scope.DoctorAsync() is { } doctor && User.IsInRole(Roles.Doctor))
        {
            DoctorName = await db.Users.Where(u => u.Id == doctor.UserId).Select(u => u.FullName).FirstOrDefaultAsync();
            Hours = await db.WorkingHours.AsNoTracking().Where(w => w.DoctorProfileId == doctor.Id).ToListAsync();
        }

        LatestMessages = await messages.AsNoTracking()
            .Where(m => !m.IsArchived)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(5)
            .ToListAsync();
    }
}
