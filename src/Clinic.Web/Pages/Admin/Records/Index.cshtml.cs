using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Records;

[Authorize(Policy = Policies.Doctor)]
public class IndexModel(ClinicDbContext db, UserManager<ApplicationUser> users, AuditLog audit, TimeProvider time, StaffScope scope) : PageModel
{
    public ApplicationUser Patient { get; private set; } = default!;
    public MedicalRecord? Record { get; private set; }
    public PatientProfile? Profile { get; private set; }
    public int? Age { get; private set; }
    public List<TreatmentSession> Sessions { get; private set; } = [];
    public List<MedicalFile> Files { get; private set; } = [];
    public Dictionary<string, string> DoctorNames { get; private set; } = [];
    public List<(DateTime Utc, double Kg)> Weights { get; private set; } = [];
    public List<(DateTime Utc, int Score)> Pains { get; private set; } = [];
    public double? LatestWeight => Sessions.FirstOrDefault(s => s.WeightKg is not null)?.WeightKg;

    /// <summary>1 for the first session, in date order (the list is newest first).</summary>
    public int SessionNumber(TreatmentSession session) => Sessions.Count - Sessions.IndexOf(session);

    public async Task<IActionResult> OnGetAsync(string patientId)
    {
        var patient = await RecordHeader.FindPatientAsync(users, patientId);
        if (patient is null)
        {
            return NotFound();
        }
        Patient = patient;

        Record = await db.MedicalRecords.AsNoTracking().FirstOrDefaultAsync(r => r.PatientUserId == patientId);
        Profile = await db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == patientId);
        Age = Profile?.AgeOn(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
        Sessions = await scope.Sessions(db.TreatmentSessions).AsNoTracking()
            .Include(s => s.Points)
            .Where(s => s.PatientUserId == patientId)
            .OrderByDescending(s => s.DateUtc)
            .ToListAsync();
        Files = await db.MedicalFiles.AsNoTracking()
            .Where(f => f.PatientUserId == patientId)
            .OrderByDescending(f => f.UploadedUtc)
            .Take(8)
            .ToListAsync();

        var doctorIds = Sessions.Select(s => s.DoctorUserId).Distinct().ToList();
        DoctorNames = await db.Users.Where(u => doctorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        var measured = Sessions.Where(s => s.WeightKg is not null).OrderBy(s => s.DateUtc).Select(s => (s.DateUtc, s.WeightKg!.Value)).ToList();
        if (Record?.WeightKg is double baseline)
        {
            // The first-visit weight comes before every session, even when the record was typed in later.
            var first = Sessions.Count > 0 ? Sessions.Min(s => s.DateUtc) : Record.CreatedUtc;
            Weights.Add((first < Record.CreatedUtc ? first.AddDays(-1) : Record.CreatedUtc, baseline));
        }
        Weights.AddRange(measured);
        Pains = Sessions.Where(s => s.PainScore is not null).OrderBy(s => s.DateUtc).Select(s => (s.DateUtc, s.PainScore!.Value)).ToList();
        Weights = Weights.OrderBy(w => w.Utc).ToList();

        await audit.WriteAsync(AuditAction.ViewRecord, patientId);
        return Page();
    }
}
