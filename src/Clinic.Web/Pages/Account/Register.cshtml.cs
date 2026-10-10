using System.ComponentModel.DataAnnotations;
using Clinic.Domain;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account;

/// <summary>Patient self-registration.</summary>
[Clinic.Web.Security.BotCheckAttribute]
public class RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await CreateUserAsync(userManager, Input, ModelState, Roles.Patient, l);
        if (user is null)
        {
            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(Url.Content("~/"));
    }

    internal static async Task<ApplicationUser?> CreateUserAsync(
        UserManager<ApplicationUser> userManager, RegisterInput input,
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState, string? role, IStringLocalizer<SharedResource> l,
        string? nationalCodeHash = null, bool staff = false)
    {
        if (!Clinic.Web.Security.PasswordPolicy.Validate(modelState, string.Empty, input.Password, staff, input.Email,
                Clinic.Application.Common.JalaliDate.ToLatinDigits(input.PhoneNumber).Trim(), l))
        {
            return null;
        }
        if (await userManager.FindByEmailAsync(input.Email) is not null)
        {
            modelState.AddModelError(string.Empty, userManager.ErrorDescriber.DuplicateEmail(input.Email).Description);
            return null;
        }
        var mobile = Clinic.Application.Common.JalaliDate.ToLatinDigits(input.PhoneNumber).Trim();
        // A patient the clinic registered at the front desk has no login yet. Taking it over needs the
        // clinic's help until sign-in by SMS exists, otherwise anyone who knows the number could claim it.
        if (userManager.Users.Any(u => u.PhoneNumber == mobile && u.PasswordHash == null))
        {
            modelState.AddModelError(string.Empty, l["This mobile number is already registered at the clinic. Please call the clinic to activate your online account."]);
            return null;
        }

        var user = new ApplicationUser
        {
            UserName = input.Email,
            Email = input.Email,
            PhoneNumber = mobile,
            FullName = input.FullName.Trim(),
            PreferredLanguage = CulturePath.IsEnglish ? CulturePath.English : CulturePath.Persian,
            NationalCodeHash = nationalCodeHash,
        };

        var result = await userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                modelState.AddModelError(string.Empty, error.Description);
            }
            return null;
        }

        if (role is not null)
        {
            await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }
}

public class RegisterInput
{
    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "{0} is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "{0} is required.")]
    [RegularExpression(@"^\s*(09|۰۹)[0-9۰-۹]{9}\s*$", ErrorMessage = "Enter an 11-digit mobile number like 09121234567.")]
    [Display(Name = "Mobile number")]
    public string PhoneNumber { get; set; } = "";

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "{0} must be at least {2} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
