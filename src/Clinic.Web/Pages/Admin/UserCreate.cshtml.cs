using System.ComponentModel.DataAnnotations;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Clinic.Web.Pages.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

/// <summary>The admin creates an account, such as a doctor or a receptionist, and gives it its roles.</summary>
[Authorize(Policy = Policies.Admin)]
public class UserCreateModel(ClinicDbContext db, UserManager<ApplicationUser> userManager, Clinic.Web.Clinical.NationalCodeIndex nationalCodes, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    [BindProperty]
    public List<string> SelectedRoles { get; set; } = [Roles.Doctor];

    [BindProperty]
    [RegularExpression(@"^\s*[0-9۰-۹]{10}\s*$", ErrorMessage = "The national code must have exactly 10 digits.")]
    [Display(Name = "National code")]
    public string? NationalCode { get; set; }

    [BindProperty]
    [RegularExpression(@"^\s*[0-9۰-۹]{3,10}\s*$", ErrorMessage = "Enter digits only.")]
    [Display(Name = "Medical Council number")]
    public string? MedicalCouncilNumber { get; set; }

    [BindProperty]
    [StringLength(100)]
    [Display(Name = "Specialty (Persian)")]
    public string? SpecialtyFa { get; set; }

    [BindProperty]
    [StringLength(100)]
    [Display(Name = "Specialty (English)")]
    public string? SpecialtyEn { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        SelectedRoles = SelectedRoles.Where(Roles.All.Contains).Distinct().ToList();
        if (SelectedRoles.Count == 0)
        {
            ModelState.AddModelError(nameof(SelectedRoles), l["Choose at least one role."]);
        }
        var isDoctor = SelectedRoles.Contains(Roles.Doctor);
        if (isDoctor && string.IsNullOrWhiteSpace(MedicalCouncilNumber))
        {
            ModelState.AddModelError(nameof(MedicalCouncilNumber), l["{0} is required.", l["Medical Council number"]]);
        }
        if (isDoctor && string.IsNullOrWhiteSpace(NationalCode))
        {
            ModelState.AddModelError(nameof(NationalCode), l["{0} is required.", l["National code"]]);
        }
        if (await nationalCodes.InUseAsync(NationalCode, exceptUserId: null))
        {
            ModelState.AddModelError(nameof(NationalCode), l["This national code is already registered for another person."]);
        }
        if (isDoctor && await CouncilNumbers.InUseAsync(db, MedicalCouncilNumber))
        {
            ModelState.AddModelError(nameof(MedicalCouncilNumber), l["This Medical Council number is already registered for another doctor."]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await RegisterModel.CreateUserAsync(userManager, Input, ModelState, SelectedRoles[0], l, nationalCodes.Hash(NationalCode));
        if (user is null)
        {
            return Page();
        }
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        foreach (var role in SelectedRoles.Skip(1))
        {
            await userManager.AddToRoleAsync(user, role);
        }
        if (SelectedRoles.Contains(Roles.Patient))
        {
            var now = time.GetUtcNow().UtcDateTime;
            db.PatientProfiles.Add(new PatientProfile { UserId = user.Id, NationalCode = Clinic.Web.Clinical.NationalCodeIndex.Normalize(NationalCode), CreatedUtc = now, UpdatedUtc = now });
            await db.SaveChangesAsync();
        }

        if (isDoctor)
        {
            // Created by the admin, so approved at once.
            db.Doctors.Add(new DoctorProfile
            {
                UserId = user.Id,
                NationalCode = Clinic.Web.Clinical.NationalCodeIndex.Normalize(NationalCode),
                RequestedUtc = time.GetUtcNow().UtcDateTime,
                MedicalCouncilNumber = CouncilNumbers.Normalize(MedicalCouncilNumber),
                SpecialtyFa = Clean(SpecialtyFa),
                SpecialtyEn = Clean(SpecialtyEn) ?? Clean(SpecialtyFa),
                IsApproved = true,
            });
            await db.SaveChangesAsync();
            TempData["Message"] = l["The doctor's account was created. Set their working hours so patients can book with them."].Value;
            var profileId = db.Doctors.Where(d => d.UserId == user.Id).Select(d => d.Id).First();
            return RedirectToPage("./Schedule", new { DoctorId = profileId });
        }

        TempData["Message"] = l["The account was created."].Value;
        return RedirectToPage("./Users", new { Q = user.Email });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
