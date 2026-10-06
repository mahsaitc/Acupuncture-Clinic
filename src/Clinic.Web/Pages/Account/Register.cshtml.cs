using System.ComponentModel.DataAnnotations;
using Clinic.Domain;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Clinic.Web.Pages.Account;

/// <summary>Patient self-registration.</summary>
public class RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : PageModel
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

        var user = await CreateUserAsync(userManager, Input, ModelState, Roles.Patient);
        if (user is null)
        {
            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(Url.Content("~/"));
    }

    internal static async Task<ApplicationUser?> CreateUserAsync(
        UserManager<ApplicationUser> userManager, RegisterInput input,
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState, string role)
    {
        var user = new ApplicationUser
        {
            UserName = input.Email,
            Email = input.Email,
            PhoneNumber = input.PhoneNumber,
            FullName = input.FullName.Trim(),
            PreferredLanguage = CulturePath.IsEnglish ? CulturePath.English : CulturePath.Persian,
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

        await userManager.AddToRoleAsync(user, role);
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
    [RegularExpression(@"^09\d{9}$|^\+\d{8,15}$", ErrorMessage = "Enter a mobile number like 09121234567.")]
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
