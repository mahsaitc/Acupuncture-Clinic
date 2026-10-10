using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Pages.Admin.Records;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Patients;

/// <summary>Edits a patient's personal details. Open to all staff; nothing clinical is on this page.</summary>
public class EditModel(ClinicDbContext db, UserManager<ApplicationUser> users, PatientRegistration registration, IStringLocalizer<SharedResource> l) : PageModel
{
    public ApplicationUser Patient { get; private set; } = default!;

    [BindProperty]
    public PatientInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var patient = await RecordHeader.FindPatientAsync(users, id);
        if (patient is null)
        {
            return NotFound();
        }
        Patient = patient;
        Input = PatientInput.From(patient, await db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var patient = await RecordHeader.FindPatientAsync(users, id);
        if (patient is null)
        {
            return NotFound();
        }
        Patient = patient;

        var dates = await registration.ValidateAsync(Input, ModelState, existingUserId: id);
        if (!ModelState.IsValid || !await registration.UpdateAsync(patient, Input, dates, ModelState))
        {
            return Page();
        }

        TempData["Message"] = l["The patient's details were saved."].Value;
        return RedirectToPage("./Details", new { id });
    }
}
