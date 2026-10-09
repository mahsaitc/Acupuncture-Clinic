using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

/// <summary>Doctors who signed up: the admin checks their documents, then approves or rejects the account.</summary>
[Authorize(Policy = Policies.Admin)]
public class DoctorRequestsModel(
    ClinicDbContext db,
    UserManager<ApplicationUser> userManager,
    PrivateFileStore store,
    IStringLocalizer<SharedResource> l) : PageModel
{
    public List<Request> Pending { get; private set; } = [];

    public record Request(ApplicationUser User, DoctorProfile Doctor);

    /// <summary>Resource key (English text) for each kind of document.</summary>
    public static string KindName(DoctorDocumentKind kind) => kind switch
    {
        DoctorDocumentKind.MedicalLicense => "Medical licence or Medical Council card",
        DoctorDocumentKind.NationalIdCard => "National ID card",
        _ => "Other document",
    };

    public async Task OnGetAsync()
    {
        var doctors = await db.Doctors.AsNoTracking().Include(d => d.Documents)
            .Where(d => !d.IsApproved)
            .OrderBy(d => d.RequestedUtc)
            .ToListAsync();
        var ids = doctors.Select(d => d.UserId).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).ToDictionaryAsync(u => u.Id);
        Pending = doctors.Where(d => users.ContainsKey(d.UserId)).Select(d => new Request(users[d.UserId], d)).ToList();
    }

    /// <summary>Streams one of a doctor's documents to the admin.</summary>
    public async Task<IActionResult> OnGetDocumentAsync(int id, bool download = false)
    {
        var document = await db.DoctorDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        var stream = document is null ? null : store.Open(document.StoredName);
        if (stream is null)
        {
            return NotFound();
        }
        return FileResults.Private(Response, stream, document!.ContentType, l[KindName(document.Kind)], download);
    }

    public async Task<IActionResult> OnPostApproveAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        var profile = await db.Doctors.FirstOrDefaultAsync(d => d.UserId == id);
        if (user is null || profile is null)
        {
            return RedirectToPage();
        }

        profile.IsApproved = true;
        await db.SaveChangesAsync();
        if (!await userManager.IsInRoleAsync(user, Roles.Doctor))
        {
            await userManager.AddToRoleAsync(user, Roles.Doctor);
        }
        await userManager.UpdateSecurityStampAsync(user);

        TempData["Message"] = l["The doctor account was approved. Set their working hours so patients can book with them."].Value;
        return RedirectToPage("./Schedule", new { DoctorId = profile.Id });
    }

    /// <summary>Turns the request down: the account is deactivated and kept, with its documents, for the record.</summary>
    public async Task<IActionResult> OnPostRejectAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is not null && await db.Doctors.AnyAsync(d => d.UserId == id && !d.IsApproved))
        {
            user.IsActive = false;
            await userManager.UpdateAsync(user);
            await userManager.UpdateSecurityStampAsync(user);
            TempData["Message"] = l["The request was rejected and the account was deactivated."].Value;
        }
        return RedirectToPage();
    }
}
