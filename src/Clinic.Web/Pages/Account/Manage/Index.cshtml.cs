using System.ComponentModel.DataAnnotations;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account.Manage;

/// <summary>The signed-in user's own security: change the password and manage two-step login.</summary>
[Authorize]
public class IndexModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration config, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public PasswordInput Input { get; set; } = new();

    public bool TwoFactorEnabled { get; private set; }
    public bool TwoFactorRequired { get; private set; }
    public bool IsStaff { get; private set; }
    public int RecoveryCodesLeft { get; private set; }

    /// <summary>Freshly made recovery codes, shown once.</summary>
    public string[]? RecoveryCodes { get; private set; }

    public class PasswordInput
    {
        [Required(ErrorMessage = "{0} is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "{0} must be at least {2} characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }
        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }
        await LoadAsync(user);
        if (!ModelState.IsValid
            || !PasswordPolicy.Validate(ModelState, string.Empty, Input.NewPassword, IsStaff, user.Email, user.PhoneNumber, l))
        {
            return Page();
        }
        var result = await userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
        await signInManager.RefreshSignInAsync(user);
        TempData["Message"] = l["Your password was changed."].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRecoveryCodesAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }
        if (user.TwoFactorEnabled)
        {
            RecoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
        }
        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }
        await LoadAsync(user);
        if (!TwoFactorRequired)
        {
            await userManager.SetTwoFactorEnabledAsync(user, false);
            await userManager.ResetAuthenticatorKeyAsync(user);
            await signInManager.RefreshSignInAsync(user);
            TempData["Message"] = l["Two-step login was turned off."].Value;
        }
        return RedirectToPage();
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        IsStaff = await PasswordPolicy.IsStaffAsync(userManager, user);
        TwoFactorEnabled = user.TwoFactorEnabled;
        TwoFactorRequired = IsStaff && TwoFactor.RequiredForStaff(config);
        RecoveryCodesLeft = TwoFactorEnabled ? await userManager.CountRecoveryCodesAsync(user) : 0;
    }
}
