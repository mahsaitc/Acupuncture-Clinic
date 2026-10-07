using Clinic.Application.Common;
using Clinic.Web.Localization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Patients;

/// <summary>The receptionist (or doctor) registers a patient at the front desk.</summary>
public class CreateModel(PatientRegistration registration, ClinicTime clock, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public PatientInput Input { get; set; } = new();

    public void OnGet()
    {
        // Most patients are registered on the day of their first visit.
        Input.FirstVisitDate = DisplayFormat.DateInput(clock.Today(time.GetUtcNow().UtcDateTime));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var dates = await registration.ValidateAsync(Input, ModelState, existingUserId: null);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await registration.CreateAsync(Input, dates, User.FindFirstValue(ClaimTypes.NameIdentifier)!, ModelState);
        if (user is null)
        {
            return Page();
        }

        TempData["Message"] = l["The patient was registered."].Value;
        return RedirectToPage("./Details", new { id = user.Id });
    }
}
