using Clinic.Domain;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Clinic.Web.Pages.Admin.Records;

public sealed record RecordHeader(ApplicationUser Patient, string Title, string Tab)
{
    /// <summary>Finds a user who is a patient, or null.</summary>
    public static async Task<ApplicationUser?> FindPatientAsync(UserManager<ApplicationUser> users, string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        var user = await users.FindByIdAsync(id);
        return user is not null && await users.IsInRoleAsync(user, Roles.Patient) ? user : null;
    }
}
