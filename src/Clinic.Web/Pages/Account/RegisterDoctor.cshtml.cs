using System.ComponentModel.DataAnnotations;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account;

/// <summary>Doctor registration. The account works as a patient account until an admin approves it.</summary>
public class RegisterDoctorModel(UserManager<ApplicationUser> userManager, ClinicDbContext db, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    [BindProperty]
    [Required(ErrorMessage = "{0} is required.")]
    [RegularExpression(@"^\d{3,10}$", ErrorMessage = "Enter digits only.")]
    [Display(Name = "Medical Council number")]
    public string MedicalCouncilNumber { get; set; } = "";

    [BindProperty]
    [StringLength(100)]
    [Display(Name = "Specialty")]
    public string? Specialty { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await RegisterModel.CreateUserAsync(userManager, Input, ModelState, Roles.Patient, l);
        if (user is null)
        {
            return Page();
        }

        db.Doctors.Add(new DoctorProfile
        {
            UserId = user.Id,
            MedicalCouncilNumber = MedicalCouncilNumber,
            SpecialtyFa = Specialty,
            SpecialtyEn = Specialty,
            IsApproved = false,
        });
        await db.SaveChangesAsync();

        TempData["Message"] = l["Your request was sent. You can log in once the clinic admin approves your account."].Value;
        return RedirectToPage("./Login");
    }
}
