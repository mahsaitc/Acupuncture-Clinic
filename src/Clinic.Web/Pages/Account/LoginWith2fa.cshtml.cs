using System.ComponentModel.DataAnnotations;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account;

/// <summary>The second step of logging in: the code from the authenticator app, or one recovery code.</summary>
public class LoginWith2faModel(SignInManager<ApplicationUser> signInManager, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(20)]
    [Display(Name = "Code from the app")]
    public string Code { get; set; } = "";

    [BindProperty]
    [Display(Name = "Don't ask again on this computer for 30 days")]
    public bool RememberMachine { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool RememberMe { get; set; }

    /// <summary>True to enter a recovery code instead of the app code.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Recovery { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // Only reachable between the password and the code.
        return await signInManager.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToPage("./Login") : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("./Login");
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var code = Clinic.Application.Common.JalaliDate.ToLatinDigits(Code).Replace(" ", "").Trim();
        var result = Recovery
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(code)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(code, RememberMe, RememberMachine);
        if (result.Succeeded)
        {
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/"));
        }
        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? l["Too many failed attempts. Try again in 15 minutes."]
            : l["The code is not correct."]);
        return Page();
    }
}
