using Microsoft.AspNetCore.Identity;

namespace Clinic.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = default!;

    /// <summary>"fa" or "en". Used for the UI and, later, for SMS and email.</summary>
    public string PreferredLanguage { get; set; } = "fa";

    /// <summary>Admins can deactivate an account without deleting its history.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
