using Clinic.Application.Common;
using Clinic.Web.Localization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Patients;

/// <summary>The receptionist (or doctor) registers a patient at the front desk.</summary>
public class CreateModel(PatientRegistration registration, Clinic.Web.Clinical.StaffScope scope, ClinicTime clock, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public PatientInput Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Most patients are registered on the day of their first visit.
        Input.FirstVisitDate = DisplayFormat.DateInput(clock.Today(time.GetUtcNow().UtcDateTime));
        // A doctor registering a patient is that patient's doctor; with one doctor in the clinic there is no choice.
        var doctors = await scope.DoctorsAsync();
        Input.DoctorId = (await scope.DoctorAsync())?.Id is int own && doctors.Any(d => d.Id == own) && User.IsInRole(Clinic.Domain.Roles.Doctor)
            ? own
            : doctors.Count == 1 ? doctors[0].Id : null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (scope.IsOwnOnly && Input.DoctorId is null)
        {
            Input.DoctorId = (await scope.DoctorAsync())?.Id;
        }
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
