using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Clinic.Web.Pages.Admin.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Patients;

/// <summary>
/// The case summary: every doctor who treated the patient, how many sessions each held, and the points and
/// treatments each used. The admin sees every doctor; a doctor sees only their own part. Reception does not open it.
/// </summary>
[Authorize(Roles = $"{Roles.Admin},{Roles.Doctor}")]
public class SummaryModel(ClinicDbContext db, UserManager<ApplicationUser> users, AuditLog audit, StaffScope scope) : PageModel
{
    /// <summary>True when the page shows only the signed-in doctor's own sessions.</summary>
    public bool IsOwnOnly => scope.IsOwnOnly;

    public ApplicationUser Patient { get; private set; } = default!;
    public PatientProfile? Profile { get; private set; }
    public List<DoctorPart> Doctors { get; private set; } = [];
    public int SessionCount => Doctors.Sum(d => d.Sessions.Count);

    public sealed record DoctorPart(
        string Name,
        int Visits,
        List<TreatmentSession> Sessions,
        List<(SessionType Type, int Count)> Treatments,
        List<(string Label, int Count)> Points);

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var patient = await RecordHeader.FindPatientAsync(users, id);
        if (patient is null)
        {
            return NotFound();
        }
        Patient = patient;
        Profile = await db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id);

        var sessions = await scope.Sessions(db.TreatmentSessions).AsNoTracking().Include(s => s.Points)
            .Where(s => s.PatientUserId == id)
            .OrderBy(s => s.DateUtc)
            .ToListAsync();
        // Completed visits per doctor, by the doctor's user id.
        var visits = await db.Appointments.AsNoTracking()
            .Where(a => a.PatientUserId == id && a.Status == AppointmentStatus.Completed)
            .Join(db.Doctors, a => a.DoctorProfileId, d => d.Id, (a, d) => d.UserId)
            .ToListAsync();

        if (scope.IsOwnOnly)
        {
            visits = visits.Where(v => v == scope.UserId).ToList();
        }

        var doctorIds = sessions.Select(s => s.DoctorUserId).Concat(visits).Distinct().ToList();
        var names = await db.Users.Where(u => doctorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        Doctors = doctorIds
            .Select(doctorId =>
            {
                var own = sessions.Where(s => s.DoctorUserId == doctorId).ToList();
                return new DoctorPart(
                    names.GetValueOrDefault(doctorId, "-"),
                    visits.Count(v => v == doctorId),
                    own,
                    own.GroupBy(s => s.Type).Select(g => (g.Key, g.Count())).OrderByDescending(t => t.Item2).ToList(),
                    own.SelectMany(s => s.Points)
                        .GroupBy(p => p.Code ?? p.Label)
                        .Select(g => (g.First().Label, g.Count()))
                        .OrderByDescending(p => p.Item2).ThenBy(p => p.Label)
                        .ToList());
            })
            .OrderByDescending(d => d.Sessions.Count)
            .ToList();

        await audit.WriteAsync(AuditAction.ViewRecord, id);
        return Page();
    }
}
