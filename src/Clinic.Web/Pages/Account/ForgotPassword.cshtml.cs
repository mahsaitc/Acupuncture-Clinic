using System.ComponentModel.DataAnnotations;
using System.Text;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Media;
using Clinic.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account;

/// <summary>Emails a link to choose a new password. The answer is the same whether or not the address has an account.</summary>
[BotCheckAttribute]
public class ForgotPasswordModel(UserManager<ApplicationUser> userManager, EmailSender email, SiteContentProvider siteContent, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "{0} is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    public bool Sent { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Email.Trim());
        if (user is { IsActive: true, Email: not null } && await userManager.HasPasswordAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = Seo.SiteUrl.Origin(HttpContext) + Url.Page("/Account/ResetPassword", new { userId = user.Id, code });
            var clinic = (await siteContent.BrandAsync()).Name;
            await email.SendAsync(user.Email, l["Choose a new password - {0}", clinic], string.Join("\n\n",
                l["Hello {0},", user.FullName],
                l["To choose a new password for your account at {0}, open this link within two hours:", clinic],
                link,
                l["If you did not ask for this, ignore this email; your password stays the same."]));
        }
        Sent = true;
        return Page();
    }
}
