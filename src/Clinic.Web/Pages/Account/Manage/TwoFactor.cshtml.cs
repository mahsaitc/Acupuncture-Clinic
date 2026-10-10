using System.ComponentModel.DataAnnotations;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Media;
using Clinic.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account.Manage;

/// <summary>Sets up two-step login: scan the QR code with an authenticator app, then confirm with its first code.</summary>
[Authorize]
public class TwoFactorModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, SiteContentProvider siteContent, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool Required { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "{0} is required.")]
    [RegularExpression(@"^\s*[0-9۰-۹]{6}\s*$", ErrorMessage = "Enter the 6-digit code from the app.")]
    [Display(Name = "Code from the app")]
    public string Code { get; set; } = "";

    public string Key { get; private set; } = "";
    public string QrSvg { get; private set; } = "";
    public string[]? RecoveryCodes { get; private set; }

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

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        var code = Clinic.Application.Common.JalaliDate.ToLatinDigits(Code).Trim();
        if (!await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            ModelState.AddModelError(nameof(Code), l["The code is not correct. Check that the phone's time is right and try the newest code."]);
            await LoadAsync(user);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        RecoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
        await signInManager.RefreshSignInAsync(user);
        return Page();
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        // The key stays the same across reloads, so a code scanned a moment ago keeps working; it is renewed when
        // two-step login is turned off. Once on, the same key is shown again to add it to another phone.
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }
        Key = TwoFactor.FormatKey(key!);
        // Authenticator apps show the issuer next to the code; the English name reads well in every app.
        var issuer = (await siteContent.GetAsync()).ClinicNameEn is { Length: > 0 } name ? name : "Acupuncture Clinic";
        QrSvg = TwoFactor.QrSvg(TwoFactor.AuthenticatorUri(issuer, user.Email ?? user.UserName ?? "", key!));
    }
}
