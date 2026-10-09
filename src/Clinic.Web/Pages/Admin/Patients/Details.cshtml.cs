using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Patients;

public class DetailsModel(ClinicDbContext db, UserManager<ApplicationUser> userManager, Clinic.Web.Clinical.StaffScope scope,
    Clinic.Web.Clinical.VisitLog visitLog) : PageModel
{
    /// <summary>Range of the visit list (Jalali or Gregorian input); empty means every visit.</summary>
    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    /// <summary>Dates the patient came in (held appointments and treatment sessions), newest first.</summary>
    public List<Clinic.Web.Clinical.Visit> Visits { get; private set; } = [];
    public DateOnly VisitsFrom { get; private set; }
    public DateOnly VisitsTo { get; private set; }

    public ApplicationUser Patient { get; private set; } = default!;
    public List<Appointment> Appointments { get; private set; } = [];
    public List<ContactMessage> Messages { get; private set; } = [];
    public PatientProfile? Profile { get; private set; }

    /// <summary>Doctor names by profile id, to show who saw the patient at each appointment.</summary>
    public Dictionary<int, string> DoctorNames { get; private set; } = [];

    public bool ShowNotes => scope.CanReadDoctorNotes;
    public bool CanSeeSummary => scope.IsAdmin || User.IsInRole(Roles.Doctor);

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

        Appointments = await (await scope.AppointmentsAsync(db.Appointments)).AsNoTracking()
            .Include(a => a.Service)
            .Where(a => a.PatientUserId == id)
            .OrderByDescending(a => a.StartUtc)
            .Take(100)
            .ToListAsync();

        DoctorNames = (await scope.DoctorsAsync()).ToDictionary(d => d.Id, d => d.Name);
        var missing = Appointments.Select(a => a.DoctorProfileId).Where(id => !DoctorNames.ContainsKey(id)).Distinct().ToList();
        if (missing.Count > 0)
        {
            // Doctors who have left the clinic are still named on their past appointments.
            foreach (var d in await db.Doctors.Where(d => missing.Contains(d.Id)).Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d.Id, u.FullName }).ToListAsync())
            {
                DoctorNames[d.Id] = d.FullName;
            }
        }

        VisitsTo = Clinic.Web.Localization.DisplayFormat.TryParseDateInput(To, out var to) ? to : visitLog.Today;
        VisitsFrom = Clinic.Web.Localization.DisplayFormat.TryParseDateInput(From, out var from) ? from : new DateOnly(2000, 1, 1);
        if (VisitsTo < VisitsFrom)
        {
            (VisitsFrom, VisitsTo) = (VisitsTo, VisitsFrom);
        }
        Visits = (await visitLog.BetweenAsync(VisitsFrom, VisitsTo, id)).OrderByDescending(v => v.Utc).ToList();
        if (string.IsNullOrWhiteSpace(From))
        {
            // The printed report starts at the first visit rather than at year 2000.
            VisitsFrom = Visits.Count > 0 ? Visits[^1].Day : visitLog.Day(user.CreatedUtc);
        }
        foreach (var d in Visits.Select(v => v.DoctorProfileId).OfType<int>().Where(d => !DoctorNames.ContainsKey(d)).Distinct().ToList())
        {
            DoctorNames[d] = await db.Doctors.Where(x => x.Id == d).Join(db.Users, x => x.UserId, u => u.Id, (x, u) => u.FullName).FirstOrDefaultAsync() ?? "-";
        }

        Messages = await (await scope.MessagesAsync(db.ContactMessages)).AsNoTracking()
            .Where(m => m.UserId == id)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(20)
            .ToListAsync();

        if (User.IsInRole(Roles.Doctor))
        {
            var sessions = scope.Sessions(db.TreatmentSessions).Where(s => s.PatientUserId == id);
            Clinical = new ClinicalSummary(
                await sessions.CountAsync(),
                await db.MedicalFiles.CountAsync(f => f.PatientUserId == id),
                await sessions.MaxAsync(s => (DateTime?)s.DateUtc));
        }

        return Page();
    }
}
