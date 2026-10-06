using Clinic.Application.Common;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

public class IndexModel(ClinicDbContext db, ClinicTime clinicTime, TimeProvider time) : PageModel
{
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

        var active = db.Appointments.Where(a => a.Status != AppointmentStatus.Cancelled);
        TodayCount = await active.CountAsync(a => a.StartUtc >= fromUtc && a.StartUtc < toUtc);
        WeekCount = await active.CountAsync(a => a.StartUtc >= fromUtc && a.StartUtc < weekUtc);

        var patientRoleId = await db.Roles.Where(r => r.Name == Roles.Patient).Select(r => r.Id).FirstOrDefaultAsync();
        PatientCount = await db.UserRoles.CountAsync(ur => ur.RoleId == patientRoleId);
        UnreadMessages = await db.ContactMessages.CountAsync(m => m.ReadUtc == null && !m.IsArchived);
        PendingDoctors = await db.Doctors.CountAsync(d => !d.IsApproved);
        PublishedPosts = await db.Posts.CountAsync(p => p.IsPublished);

        Today = await (
                from a in active.AsNoTracking().Include(a => a.Service)
                join p in db.Users on a.PatientUserId equals p.Id
                where a.StartUtc >= fromUtc && a.StartUtc < toUtc
                orderby a.StartUtc
                select new TodayRow(a.StartUtc, p.FullName, a.Service, a.Status))
            .ToListAsync();

        LatestMessages = await db.ContactMessages.AsNoTracking()
            .Where(m => !m.IsArchived)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(5)
            .ToListAsync();
    }
}
