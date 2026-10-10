using System.Security.Claims;
using Clinic.Domain;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Clinic.Web.Branding;

/// <summary>Keeps the site owner's account out of the admin's reach: only the owner may change it.</summary>
public static class OwnerGuard
{
    public static async Task<bool> IsProtectedAsync(UserManager<ApplicationUser> users, ApplicationUser target, ClaimsPrincipal current) =>
        !current.IsInRole(Roles.Owner) && await users.IsInRoleAsync(target, Roles.Owner);
}
