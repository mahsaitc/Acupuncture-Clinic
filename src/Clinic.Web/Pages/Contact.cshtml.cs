using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Content;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages;

public class ContactModel(
    ClinicDbContext db,
    SiteContentProvider siteContent,
    ContactThrottle throttle,
    TimeProvider time,
    IStringLocalizer<SharedResource> l) : PageModel
{
    public SiteContent Site { get; private set; } = new();

    [BindProperty]
    public MessageInput Input { get; set; } = new();

    /// <summary>Hidden field that people never fill; bots usually do.</summary>
    [BindProperty]
    public string? Website { get; set; }

    public class MessageInput
    {
        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [RegularExpression(@"^\s*[0۰][0-9۰-۹]{10}\s*$", ErrorMessage = "Enter an 11-digit phone number like 09121234567.")]
        [Display(Name = "Mobile number")]
        public string Phone { get; set; } = "";

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(200)]
        [Display(Name = "Email (optional)")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(4000, MinimumLength = 10, ErrorMessage = "{0} must be at least {2} characters.")]
        [Display(Name = "Message")]
        public string Body { get; set; } = "";
    }

    public async Task OnGetAsync() => Site = await siteContent.GetAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        Site = await siteContent.GetAsync();

        if (!string.IsNullOrEmpty(Website))
        {
            // Pretend success so bots learn nothing.
            TempData["Message"] = l["Your message was sent. The clinic will contact you soon."].Value;
            return RedirectToPage();
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }
        if (!throttle.TryAcquire(HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError(string.Empty, l["You have sent several messages. Please try again later or call the clinic."]);
            return Page();
        }

        db.ContactMessages.Add(new ContactMessage
        {
            Name = Input.Name.Trim(),
            Phone = Clinic.Application.Common.JalaliDate.ToLatinDigits(Input.Phone).Trim(),
            Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            Subject = Input.Subject.Trim(),
            Body = Input.Body.Trim(),
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedUtc = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();

        TempData["Message"] = l["Your message was sent. The clinic will contact you soon."].Value;
        return RedirectToPage();
    }
}
