using System.ComponentModel.DataAnnotations;
using System.Text;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account;

/// <summary>Chooses a new password from the emailed link. The link works once and for two hours.</summary>
public class ResetPasswordModel(UserManager<ApplicationUser> userManager, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string UserId { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "{0} must be at least {2} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    public IActionResult OnGet() =>
        string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(Code) ? RedirectToPage("./ForgotPassword") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is not null && ModelState.IsValid)
        {
            PasswordPolicy.Validate(ModelState, string.Empty, NewPassword, await PasswordPolicy.IsStaffAsync(userManager, user), user.Email, user.PhoneNumber, l);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        IdentityResult result;
        try
        {
            result = user is null
                ? IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken())
                : await userManager.ResetPasswordAsync(user, Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code)), NewPassword);
        }
        catch (FormatException)
        {
            result = IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken());
        }
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Code == nameof(IdentityErrorDescriber.InvalidToken)
                    ? l["This link has expired or was already used. Ask for a new one."]
                    : error.Description);
            }
            return Page();
        }

        // Someone who forgot the password may have locked the account by guessing.
        await userManager.SetLockoutEndDateAsync(user!, null);
        await userManager.ResetAccessFailedCountAsync(user!);
        TempData["Message"] = l["Your password was changed. You can log in now."].Value;
        return RedirectToPage("./Login");
    }
}
