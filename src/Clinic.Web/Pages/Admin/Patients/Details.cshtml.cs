using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Patients;

public class DetailsModel(ClinicDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    public ApplicationUser Patient { get; private set; } = default!;
    public List<Appointment> Appointments { get; private set; } = [];
    public List<ContactMessage> Messages { get; private set; } = [];
    public PatientProfile? Profile { get; private set; }

    /// <summary>Counts only, and only for doctors; receptionists never see clinical data.</summary>
    public ClinicalSummary? Clinical { get; private set; }

    public sealed record ClinicalSummary(int Sessions, int Files, DateTime? LastSessionUtc);

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null || !await userManager.IsInRoleAsync(user, Roles.Patient))
        {
            return NotFound();
        }
        Patient = user;
        Profile = await db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id);

        Appointments = await db.Appointments.AsNoTracking()
            .Include(a => a.Service)
            .Where(a => a.PatientUserId == id)
            .OrderByDescending(a => a.StartUtc)
            .Take(100)
            .ToListAsync();

        Messages = await db.ContactMessages.AsNoTracking()
            .Where(m => m.UserId == id)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(20)
            .ToListAsync();

        if (User.IsInRole(Roles.Doctor))
        {
            var sessions = db.TreatmentSessions.Where(s => s.PatientUserId == id);
            Clinical = new ClinicalSummary(
                await sessions.CountAsync(),
                await db.MedicalFiles.CountAsync(f => f.PatientUserId == id),
                await sessions.MaxAsync(s => (DateTime?)s.DateUtc));
        }

        return Page();
    }
}
