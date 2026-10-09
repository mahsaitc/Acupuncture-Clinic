using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Doctors;

/// <summary>
/// One doctor's patients. The admin opens their records from here; reception sees the list and the patients' contact
/// details, but not the records.
/// </summary>
public class PatientsModel(ClinicDbContext db, StaffScope scope, TimeProvider time) : PageModel
{
    private const int MaxRows = 300;

    public string DoctorName { get; private set; } = "";
    public DoctorProfile Doctor { get; private set; } = default!;
    public bool CanOpenRecords => scope.IsAdmin && User.IsInRole(Roles.Doctor);
    public bool CanSeeSummary => scope.IsAdmin;
    public List<Row> Rows { get; private set; } = [];

    public record Row(string Id, string FullName, string? Phone, int Sessions, DateTime? LastUtc, DateTime? NextUtc);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (scope.IsOwnOnly)
        {
            return Forbid();
        }
        var doctor = await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doctor is null)
        {
            return NotFound();
        }
        Doctor = doctor;
        DoctorName = await db.Users.Where(u => u.Id == doctor.UserId).Select(u => u.FullName).FirstAsync();

        var patientRoleId = await db.Roles.Where(r => r.Name == Roles.Patient).Select(r => r.Id).FirstOrDefaultAsync();
        var patients = db.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == patientRoleId));
        var now = time.GetUtcNow().UtcDateTime;
        var list = await scope.PatientsOf(patients, doctor.Id, doctor.UserId)
            .OrderBy(u => u.FullName)
            .Take(MaxRows)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.PhoneNumber,
                Sessions = db.TreatmentSessions.Count(s => s.PatientUserId == u.Id && s.DoctorUserId == doctor.UserId),
                Next = db.Appointments
                    .Where(a => a.PatientUserId == u.Id && a.DoctorProfileId == doctor.Id && a.StartUtc > now && a.Status != AppointmentStatus.Cancelled)
                    .OrderBy(a => a.StartUtc).Select(a => (DateTime?)a.StartUtc).FirstOrDefault(),
            })
            .ToListAsync();
        foreach (var p in list)
        {
            var last = await DoctorVisits.LastAsync(db, doctor.Id, doctor.UserId, now, p.Id);
            Rows.Add(new Row(p.Id, p.FullName, p.PhoneNumber, p.Sessions, last?.Utc, p.Next));
        }
        Rows = Rows.OrderByDescending(r => r.LastUtc ?? DateTime.MinValue).ThenBy(r => r.FullName).ToList();
        return Page();
    }
}
