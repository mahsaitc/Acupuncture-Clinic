using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Doctors;

/// <summary>
/// One doctor's page: their upcoming appointments and their patients. The admin also sees the doctor's sign-up file and
/// documents and opens the patients' records from here; reception sees the appointments and the patient list, but not
/// the records or the doctor's file.
/// </summary>
public class PatientsModel(ClinicDbContext db, StaffScope scope, TimeProvider time) : PageModel
{
    private const int MaxRows = 300;

    public string DoctorName { get; private set; } = "";
    public DoctorProfile Doctor { get; private set; } = default!;
    public Clinic.Infrastructure.Identity.ApplicationUser DoctorUser { get; private set; } = default!;
    public bool ShowFile => scope.IsAdmin;
    public List<Appointment> Upcoming { get; private set; } = [];
    public Dictionary<string, string> UpcomingPatients { get; private set; } = [];
    public HashSet<string> Openable { get; private set; } = [];
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
        var doctor = await db.Doctors.AsNoTracking().Include(d => d.Documents).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor is null)
        {
            return NotFound();
        }
        Doctor = doctor;
        DoctorUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == doctor.UserId);
        DoctorName = DoctorUser.FullName;

        var patientRoleId = await db.Roles.Where(r => r.Name == Roles.Patient).Select(r => r.Id).FirstOrDefaultAsync();
        var patients = db.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == patientRoleId));
        var now = time.GetUtcNow().UtcDateTime;
        Upcoming = await db.Appointments.AsNoTracking().Include(a => a.Service)
            .Where(a => a.DoctorProfileId == doctor.Id && a.EndUtc > now && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.StartUtc)
            .Take(100)
            .ToListAsync();
        var upcomingIds = Upcoming.Select(a => a.PatientUserId).Distinct().ToList();
        UpcomingPatients = await db.Users.Where(u => upcomingIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        Openable = await scope.OpenablePatientIdsAsync(upcomingIds);

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
