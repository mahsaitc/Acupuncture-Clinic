using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Patients;

public class IndexModel(ClinicDbContext db, TimeProvider time) : PageModel
{
    private const int PageSize = 30;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true, Name = PageQueryKey)]
    public int PageNumber { get; set; } = 1;

    private const string PageQueryKey = "p";

    public int Total { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public List<Row> Rows { get; private set; } = [];

    public record Row(string Id, string FullName, string? Phone, string? Email, bool IsActive, DateTime CreatedUtc, int Visits, DateTime? NextUtc);

    public async Task OnGetAsync()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var patientRoleId = await db.Roles.Where(r => r.Name == Roles.Patient).Select(r => r.Id).FirstOrDefaultAsync();

        var query = from u in db.Users
                    join ur in db.UserRoles on u.Id equals ur.UserId
                    where ur.RoleId == patientRoleId
                    select u;
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            query = query.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q) || u.PhoneNumber!.Contains(q));
        }

        Total = await query.CountAsync();
        PageNumber = Math.Clamp(PageNumber, 1, PageCount);

        Rows = await query
            .OrderByDescending(u => u.CreatedUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new Row(
                u.Id, u.FullName, u.PhoneNumber, u.Email, u.IsActive, u.CreatedUtc,
                db.Appointments.Count(a => a.PatientUserId == u.Id && a.Status == AppointmentStatus.Completed),
                db.Appointments.Where(a => a.PatientUserId == u.Id && a.StartUtc > now && a.Status != AppointmentStatus.Cancelled)
                    .OrderBy(a => a.StartUtc).Select(a => (DateTime?)a.StartUtc).FirstOrDefault()))
            .ToListAsync();
    }
}
