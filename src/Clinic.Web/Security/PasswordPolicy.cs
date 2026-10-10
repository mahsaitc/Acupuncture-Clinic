using Clinic.Domain;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Security;

/// <summary>
/// Rules on top of Identity's own (8 characters, a digit, upper and lower case) for passwords people choose:
/// never a well-known password or one made of the user's email or mobile, and 12 characters for staff,
/// who can open medical records. Checked where a password is chosen, so the passwords set from the
/// server's configuration at start-up (owner, first admin) are never refused or reset by it.
/// </summary>
public static class PasswordPolicy
{
    public const int StaffMinLength = 12;

    public static readonly string[] StaffRoles = [Roles.Owner, Roles.Admin, Roles.Doctor, Roles.Receptionist];

    // The most used passwords that still pass Identity's own rules, as found in public breach lists.
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password1", "Password12", "Password123", "Password1234", "Password12345", "Password123!", "Password1!", "Passw0rd", "Passw0rd!",
        "P@ssw0rd", "P@ssword1", "P@ssw0rd1", "P@ssw0rd123", "Qwerty123", "Qwerty1234", "Qwerty123!", "Qwerty12345", "Abc12345",
        "Abcd1234", "Abcd@1234", "Aa123456", "Aa1234567", "Aa12345678", "Aa@123456", "Admin123", "Admin1234", "Admin12345",
        "Admin@123", "Welcome1", "Welcome123", "Welcome@123", "Iloveyou1", "Sunshine1", "Princess1", "Football1", "Monkey123",
        "Dragon123", "Master123", "Letmein1", "Zxcvbnm1", "Asdf1234", "Asdfgh123", "1qaz2wsx", "1Qaz2wsx", "1Qaz@2wsx",
        "Changeme1", "Test1234", "Test@1234", "User1234", "Iran1234", "Iran12345", "Tehran123", "Tehran1234", "Shiraz123",
        "Ali12345", "Mohammad123", "Clinic123", "Clinic1234", "Doctor123", "Doctor1234", "Doctor@123", "Mahsa123", "Mahsa1234",
        "Hassani123", "Password2024", "Password2025", "Password2026", "Summer2024", "Summer2025", "Winter2025", "Spring2025",
        "Qwertyuiop1", "Q1w2e3r4", "Q1w2e3r4t5", "Q1w2e3r4t5y6", "Zaq12wsx", "Zaq1@wsx", "Aa123456789", "Abc123456",
        "Password123456", "Administrator1", "Admin123456", "Admin1234567", "Doctor123456", "Clinic123456", "Iran123456789",
    };

    /// <summary>Error messages for <paramref name="password"/>, empty when it is acceptable.</summary>
    public static List<string> Check(string password, bool staff, string? email, string? mobile, IStringLocalizer l)
    {
        var errors = new List<string>();
        if (staff && password.Length < StaffMinLength)
        {
            errors.Add(l["Staff passwords must be at least {0} characters.", StaffMinLength]);
        }
        if (Common.Contains(password.Trim()))
        {
            errors.Add(l["This password is too common and easy to guess. Choose another one."]);
        }
        var name = email?.Split('@')[0];
        if ((name is { Length: >= 4 } && password.Contains(name, StringComparison.OrdinalIgnoreCase))
            || (mobile is { Length: >= 8 } && password.Contains(mobile[^8..], StringComparison.Ordinal)))
        {
            errors.Add(l["The password must not contain your email or mobile number."]);
        }
        return errors;
    }

    public static async Task<bool> IsStaffAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return roles.Any(StaffRoles.Contains);
    }

    /// <summary>Adds the errors of <see cref="Check"/> to the page; returns false when there were any.</summary>
    public static bool Validate(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState, string key,
        string password, bool staff, string? email, string? mobile, IStringLocalizer l)
    {
        var errors = Check(password, staff, email, mobile, l);
        foreach (var error in errors)
        {
            modelState.AddModelError(key, error);
        }
        return errors.Count == 0;
    }
}
