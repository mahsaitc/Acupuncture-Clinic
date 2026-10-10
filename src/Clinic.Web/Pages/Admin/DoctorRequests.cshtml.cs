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
        // Deactivated ones are requests rejected before rejecting deleted the account; they stay listed so they can be deleted.
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
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

    /// <summary>
    /// Turns the request down: the account, its doctor profile and its documents are deleted, so the person signs up again
    /// with complete documents (their email, national code and Medical Council number become free). An account that already
    /// has a role or any clinic history is never deleted; it is only deactivated.
    /// </summary>
    public async Task<IActionResult> OnPostRejectAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        var profile = await db.Doctors.Include(d => d.Documents).FirstOrDefaultAsync(d => d.UserId == id && !d.IsApproved);
        if (user is null || profile is null)
        {
            return RedirectToPage();
        }

        if (await HasHistoryAsync(user, profile))
        {
            user.IsActive = false;
            await userManager.UpdateAsync(user);
            await userManager.UpdateSecurityStampAsync(user);
            TempData["Message"] = l["The account has clinic history, so it was deactivated instead of deleted."].Value;
            return RedirectToPage();
        }

        var files = profile.Documents.Select(d => d.StoredName).ToList();
        db.DoctorDocuments.RemoveRange(profile.Documents);
        db.Doctors.Remove(profile);
        await db.SaveChangesAsync();
        var deleted = await userManager.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            TempData["Error"] = string.Join(" ", deleted.Errors.Select(e => e.Description));
            return RedirectToPage();
        }
        foreach (var file in files)
        {
            store.Delete(file);
        }

        TempData["Message"] = l["The request was rejected and the account and its documents were deleted. The doctor can sign up again."].Value;
        return RedirectToPage();
    }

    /// <summary>Anything that must not disappear with the account: roles, appointments, treatment, posts, messages.</summary>
    private async Task<bool> HasHistoryAsync(ApplicationUser user, DoctorProfile profile) =>
        (await userManager.GetRolesAsync(user)).Count > 0
        || await db.Appointments.AnyAsync(a => a.DoctorProfileId == profile.Id || a.PatientUserId == user.Id)
        || await db.TreatmentSessions.AnyAsync(t => t.DoctorUserId == user.Id || t.PatientUserId == user.Id)
        || await db.PatientProfiles.AnyAsync(p => p.UserId == user.Id || p.DoctorProfileId == profile.Id)
        || await db.Posts.AnyAsync(p => p.AuthorUserId == user.Id)
        || await db.ContactMessages.AnyAsync(m => m.UserId == user.Id || m.DoctorProfileId == profile.Id)
        || await db.MessageReplies.AnyAsync(r => r.AuthorUserId == user.Id)
        || await db.MedicalFiles.AnyAsync(f => f.UploadedByUserId == user.Id || f.PatientUserId == user.Id);
}
