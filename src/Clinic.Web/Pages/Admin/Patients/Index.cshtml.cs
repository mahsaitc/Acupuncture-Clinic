using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Patients;

public class IndexModel(ClinicDbContext db, Clinic.Web.Clinical.StaffScope scope, Clinic.Web.Clinical.NationalCodeIndex nationalCodes, TimeProvider time) : PageModel
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

    /// <summary>The admin and reception see each patient's doctor; a doctor's own list has no need for it.</summary>
    public bool ShowDoctor => !scope.IsOwnOnly;

    public Dictionary<int, string> DoctorNames { get; private set; } = [];

    /// <summary>National codes by patient id (stored encrypted, so read for the rows on this page only).</summary>
    public Dictionary<string, string?> NationalCodes { get; private set; } = [];

    /// <summary>
    /// <paramref name="DoctorId"/> is the treating doctor set on the patient's file, or else the doctor of their latest
    /// appointment (<paramref name="DoctorFromBooking"/>).
    /// </summary>
    public record Row(string Id, string FullName, string? Phone, string? Email, bool IsActive, DateTime CreatedUtc, int Visits, DateTime? NextUtc,
        int? DoctorId, bool DoctorFromBooking);

    public async Task<IActionResult> OnGetAsync()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var patientRoleId = await db.Roles.Where(r => r.Name == Roles.Patient).Select(r => r.Id).FirstOrDefaultAsync();

        var query = from u in db.Users
                    join ur in db.UserRoles on u.Id equals ur.UserId
                    where ur.RoleId == patientRoleId
                    select u;
        query = await scope.PatientsAsync(query);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            var digits = Clinic.Web.Clinical.NationalCodeIndex.Normalize(q);
            // A 10-digit search is a national code; codes are encrypted, so they are matched by their keyed hash.
            var codeHash = digits is { Length: 10 } ? nationalCodes.Hash(digits) : null;
            if (codeHash is not null)
            {
                var byCode = await query.Where(u => u.NationalCodeHash == codeHash).Select(u => u.Id).ToListAsync();
                if (byCode.Count == 1)
                {
                    // One patient: go straight to their page with the appointment and visit history.
                    return RedirectToPage("./Details", new { id = byCode[0] });
                }
            }
            var phone = digits ?? q;
            query = query.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q) || u.PhoneNumber!.Contains(phone)
                || (codeHash != null && u.NationalCodeHash == codeHash));
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
                    .OrderBy(a => a.StartUtc).Select(a => (DateTime?)a.StartUtc).FirstOrDefault(),
                db.PatientProfiles.Where(p => p.UserId == u.Id).Select(p => p.DoctorProfileId).FirstOrDefault()
                    ?? db.Appointments.Where(a => a.PatientUserId == u.Id && a.Status != AppointmentStatus.Cancelled)
                        .OrderByDescending(a => a.StartUtc).Select(a => (int?)a.DoctorProfileId).FirstOrDefault(),
                db.PatientProfiles.Where(p => p.UserId == u.Id).Select(p => p.DoctorProfileId).FirstOrDefault() == null))
            .ToListAsync();

        var ids = Rows.Select(r => r.Id).ToList();
        NationalCodes = (await db.PatientProfiles.AsNoTracking().Where(p => ids.Contains(p.UserId)).Select(p => new { p.UserId, p.NationalCode }).ToListAsync())
            .ToDictionary(p => p.UserId, p => p.NationalCode);

        if (ShowDoctor)
        {
            // Doctors who have left the clinic are still named.
            DoctorNames = await db.Doctors.AsNoTracking()
                .Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d.Id, u.FullName })
                .ToDictionaryAsync(d => d.Id, d => d.FullName);
        }
        return Page();
    }
}
