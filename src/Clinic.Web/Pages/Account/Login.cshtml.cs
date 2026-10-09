using System.ComponentModel.DataAnnotations;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account;

public class LoginModel(SignInManager<ApplicationUser> signInManager, Clinic.Infrastructure.Data.ClinicDbContext db, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "{0} is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await signInManager.UserManager.FindByEmailAsync(Input.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, l["Email or password is incorrect."]);
            return Page();
        }

        // A doctor who signed up waits for the admin: until then the account has no role and cannot sign in.
        if (await signInManager.UserManager.CheckPasswordAsync(user, Input.Password)
            && db.Doctors.Any(d => d.UserId == user.Id && !d.IsApproved)
            && (await signInManager.UserManager.GetRolesAsync(user)).Count == 0)
        {
            ModelState.AddModelError(string.Empty, l["Your doctor account is waiting for the clinic admin to check your documents."]);
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/"));
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? l["Too many failed attempts. Try again in 15 minutes."]
            : l["Email or password is incorrect."]);
        return Page();
    }
}
