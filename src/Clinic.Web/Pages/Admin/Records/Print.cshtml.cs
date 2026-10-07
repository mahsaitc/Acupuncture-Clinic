using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Records;

/// <summary>The patient's record laid out as the clinic's paper intake form, for printing or saving as PDF.</summary>
[Authorize(Policy = Policies.Doctor)]
public class PrintModel(ClinicDbContext db, UserManager<ApplicationUser> users, AuditLog audit, SiteContentProvider site, TimeProvider time) : PageModel
{
    /// <summary>The paper form has twelve session rows; longer treatments get more.</summary>
    public const int MinSessionRows = 12;

    public ApplicationUser Patient { get; private set; } = default!;
    public PatientProfile? Profile { get; private set; }
    public MedicalRecord Record { get; private set; } = new();
    public List<TreatmentSession> Sessions { get; private set; } = [];
    public int? Age { get; private set; }
    public string? ClinicPhone { get; private set; }

    public async Task<IActionResult> OnGetAsync(string patientId)
    {
        var patient = await RecordHeader.FindPatientAsync(users, patientId);
        if (patient is null)
        {
            return NotFound();
        }
        Patient = patient;
        Profile = await db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == patientId);
        Record = await db.MedicalRecords.AsNoTracking().FirstOrDefaultAsync(r => r.PatientUserId == patientId) ?? new MedicalRecord();
        Sessions = await db.TreatmentSessions.AsNoTracking().Include(s => s.Points)
            .Where(s => s.PatientUserId == patientId)
            .OrderBy(s => s.DateUtc)
            .ToListAsync();
        Age = Profile?.AgeOn(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
        ClinicPhone = (await site.GetAsync()).Phone;

        await audit.WriteAsync(AuditAction.ViewRecord, patientId);
        return Page();
    }
}
