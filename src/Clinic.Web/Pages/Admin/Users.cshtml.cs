using System.Security.Claims;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Branding;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

[Authorize(Policy = Policies.Admin)]
public class UsersModel(ClinicDbContext db, UserManager<ApplicationUser> userManager, IStringLocalizer<SharedResource> l) : PageModel
{
    private const int PageSize = 50;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public List<Row> Rows { get; private set; } = [];

    public record Row(ApplicationUser User, IList<string> Roles, DoctorProfile? Doctor);

    public async Task OnGetAsync()
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            query = query.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q) || u.PhoneNumber!.Contains(q));
        }

        var users = await query.OrderByDescending(u => u.CreatedUtc).Take(PageSize).ToListAsync();
        var ids = users.Select(u => u.Id).ToList();
        var doctors = await db.Doctors.AsNoTracking().Where(d => ids.Contains(d.UserId)).ToDictionaryAsync(d => d.UserId);

        foreach (var user in users)
        {
            Rows.Add(new Row(user, await userManager.GetRolesAsync(user), doctors.GetValueOrDefault(user.Id)));
        }
    }

    public async Task<IActionResult> OnPostToggleRoleAsync(string id, string role)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null || !Roles.All.Contains(role) || IsSelfAdminChange(id, role) || await OwnerGuard.IsProtectedAsync(userManager, user, User))
        {
            return RedirectToPage(new { Q });
        }

        var profile = role == Roles.Doctor ? await db.Doctors.FirstOrDefaultAsync(d => d.UserId == id) : null;
        if (await userManager.IsInRoleAsync(user, role))
        {
            await userManager.RemoveFromRoleAsync(user, role);
            // A former doctor no longer appears in the booking list; their history stays.
            if (profile is not null)
            {
                profile.IsApproved = false;
            }
        }
        else
        {
            await userManager.AddToRoleAsync(user, role);
            // Giving the doctor role makes the user a bookable doctor at once.
            if (role == Roles.Doctor)
            {
                if (profile is null)
                {
                    db.Doctors.Add(new DoctorProfile { UserId = id, MedicalCouncilNumber = "", IsApproved = true });
                }
                else
                {
                    profile.IsApproved = true;
                }
            }
        }
        await db.SaveChangesAsync();
        await userManager.UpdateSecurityStampAsync(user);
        return RedirectToPage(new { Q });
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null || id == CurrentUserId || await OwnerGuard.IsProtectedAsync(userManager, user, User))
        {
            return RedirectToPage(new { Q });
        }

        user.IsActive = !user.IsActive;
        await userManager.UpdateAsync(user);
        // Signs the user out of existing sessions at the next security-stamp check.
        await userManager.UpdateSecurityStampAsync(user);
        return RedirectToPage(new { Q });
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>An admin cannot remove their own admin role and lock themselves out.</summary>
    private bool IsSelfAdminChange(string id, string role) => id == CurrentUserId && role == Roles.Admin;
}
