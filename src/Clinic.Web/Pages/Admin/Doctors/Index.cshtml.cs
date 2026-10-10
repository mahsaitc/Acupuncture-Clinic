using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Doctors;

/// <summary>
/// The clinic's doctors with their last visit. The admin also sees national codes; reception sees the basic details.
/// A doctor who is not the admin has their own patient list instead.
/// </summary>
public class IndexModel(ClinicDbContext db, StaffScope scope, TimeProvider time) : PageModel
{
    public bool ShowNationalCode => scope.IsAdmin;
    public List<Row> Rows { get; private set; } = [];

    public record Row(DoctorProfile Doctor, ApplicationUser User, int Patients, DoctorVisits.LastVisit? Last);

    public async Task<IActionResult> OnGetAsync()
    {
        if (scope.IsOwnOnly)
        {
            return Forbid();
        }
        var doctors = await db.Doctors.AsNoTracking()
            .Where(d => d.IsApproved)
            .Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { Doctor = d, User = u })
            .OrderBy(x => x.User.FullName)
            .ToListAsync();
        var patientRoleId = await db.Roles.Where(r => r.Name == Roles.Patient).Select(r => r.Id).FirstOrDefaultAsync();
        var patients = db.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == patientRoleId));
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var d in doctors)
        {
            Rows.Add(new Row(
                d.Doctor,
                d.User,
                await scope.PatientsOf(patients, d.Doctor.Id, d.User.Id).CountAsync(),
                await DoctorVisits.LastAsync(db, d.Doctor.Id, d.User.Id, now)));
        }
        return Page();
    }
}
