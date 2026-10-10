using System.ComponentModel.DataAnnotations;
using Clinic.Application.Common;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Branding;
using Clinic.Web.Clinical;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Doctors;

/// <summary>The admin edits a doctor's sign-up file: account details, Medical Council details and documents.</summary>
[Authorize(Policy = Policies.Admin)]
[RequestSizeLimit(PrivateFileStore.DocumentMaxBytes + 1024 * 1024)]
public class EditModel(
    ClinicDbContext db,
    UserManager<ApplicationUser> userManager,
    NationalCodeIndex nationalCodes,
    PrivateFileStore store,
    TimeProvider time,
    IStringLocalizer<SharedResource> l) : PageModel
{
    public DoctorProfile Doctor { get; private set; } = default!;
    public ApplicationUser DoctorUser { get; private set; } = default!;

    [BindProperty]
    public DoctorInput Input { get; set; } = new();

    public class DoctorInput
    {
        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [RegularExpression(@"^\s*(09|۰۹)[0-9۰-۹]{9}\s*$", ErrorMessage = "Enter an 11-digit mobile number like 09121234567.")]
        [Display(Name = "Mobile number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "{0} is required.")]
        [RegularExpression(@"^\s*[0-9۰-۹]{10}\s*$", ErrorMessage = "The national code must have exactly 10 digits.")]
        [Display(Name = "National code")]
        public string NationalCode { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [RegularExpression(@"^\s*[0-9۰-۹]{1,10}\s*$", ErrorMessage = "Enter digits only.")]
        [Display(Name = "Medical Council number")]
        public string MedicalCouncilNumber { get; set; } = "";

        [StringLength(100)]
        [Display(Name = "Specialty (Persian)")]
        public string? SpecialtyFa { get; set; }

        [StringLength(100)]
        [Display(Name = "Specialty (English)")]
        public string? SpecialtyEn { get; set; }

        [StringLength(2000)]
        [Display(Name = "About the doctor (Persian)")]
        public string? BioFa { get; set; }

        [StringLength(2000)]
        [Display(Name = "About the doctor (English)")]
        public string? BioEn { get; set; }
    }

    /// <summary>The site owner's file (email, name, documents) is closed to everyone but the owner.</summary>
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.HandlerArguments.TryGetValue("id", out var value) && value is int id
            && await db.Doctors.AsNoTracking().Where(d => d.Id == id).Select(d => d.UserId).FirstOrDefaultAsync() is { } userId
            && await userManager.FindByIdAsync(userId) is { } user
            && await OwnerGuard.IsProtectedAsync(userManager, user, User))
        {
            context.Result = Forbid();
            return;
        }
        await next();
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id))
        {
            return NotFound();
        }
        Input = new DoctorInput
        {
            FullName = DoctorUser.FullName,
            Email = DoctorUser.Email ?? "",
            PhoneNumber = DoctorUser.PhoneNumber,
            NationalCode = Doctor.NationalCode ?? "",
            MedicalCouncilNumber = Doctor.MedicalCouncilNumber,
            SpecialtyFa = Doctor.SpecialtyFa,
            SpecialtyEn = Doctor.SpecialtyEn,
            BioFa = Doctor.BioFa,
            BioEn = Doctor.BioEn,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id))
        {
            return NotFound();
        }
        if (await nationalCodes.InUseAsync(Input.NationalCode, DoctorUser.Id))
        {
            ModelState.AddModelError("Input.NationalCode", l["This national code is already registered for another person."]);
        }
        if (await CouncilNumbers.InUseAsync(db, Input.MedicalCouncilNumber, DoctorUser.Id))
        {
            ModelState.AddModelError("Input.MedicalCouncilNumber", l["This Medical Council number is already registered for another doctor."]);
        }
        var email = Input.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is { } other && other.Id != DoctorUser.Id)
        {
            ModelState.AddModelError("Input.Email", l["An account with this email already exists."]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = (await userManager.FindByIdAsync(DoctorUser.Id))!;
        user.FullName = Input.FullName.Trim();
        user.Email = email;
        user.UserName = email;
        user.PhoneNumber = Clean(JalaliDate.ToLatinDigits(Input.PhoneNumber ?? ""));
        user.NationalCodeHash = nationalCodes.Hash(Input.NationalCode);
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            foreach (var e in updated.Errors)
            {
                ModelState.AddModelError(string.Empty, e.Description);
            }
            return Page();
        }

        var profile = await db.Doctors.FirstAsync(d => d.Id == id);
        profile.NationalCode = NationalCodeIndex.Normalize(Input.NationalCode);
        profile.MedicalCouncilNumber = CouncilNumbers.Normalize(Input.MedicalCouncilNumber);
        profile.SpecialtyFa = Clean(Input.SpecialtyFa);
        profile.SpecialtyEn = Clean(Input.SpecialtyEn) ?? profile.SpecialtyFa;
        profile.BioFa = Clean(Input.BioFa);
        profile.BioEn = Clean(Input.BioEn);
        await db.SaveChangesAsync();

        TempData["Message"] = l["The doctor's file was saved."].Value;
        return RedirectToPage("./Patients", new { id });
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, DoctorDocumentKind kind, IFormFile? file)
    {
        if (!await LoadAsync(id))
        {
            return NotFound();
        }
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = l["Choose a file first."].Value;
            return RedirectToPage(new { id });
        }
        var saved = await store.SaveAsync(file, PrivateFileStore.DocumentMaxBytes);
        if (saved.Error != PrivateFileStore.SaveError.None)
        {
            TempData["Error"] = (saved.Error == PrivateFileStore.SaveError.TooLarge
                ? l["{0} is larger than 2 MB.", file.FileName]
                : l["{0} must be a JPG, PNG, WEBP or PDF file.", file.FileName]).Value;
            return RedirectToPage(new { id });
        }
        db.DoctorDocuments.Add(new DoctorDocument
        {
            DoctorProfileId = id,
            Kind = Enum.IsDefined(kind) ? kind : DoctorDocumentKind.OtherDocument,
            StoredName = saved.StoredName!,
            ContentType = saved.ContentType!,
            SizeBytes = saved.Size,
            UploadedUtc = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
        TempData["Message"] = l["The document was added."].Value;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteDocumentAsync(int id, int documentId)
    {
        var document = await db.DoctorDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.DoctorProfileId == id);
        if (document is not null)
        {
            db.DoctorDocuments.Remove(document);
            await db.SaveChangesAsync();
            store.Delete(document.StoredName);
            TempData["Message"] = l["The document was deleted."].Value;
        }
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var doctor = await db.Doctors.AsNoTracking().Include(d => d.Documents).FirstOrDefaultAsync(d => d.Id == id);
        var user = doctor is null ? null : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == doctor.UserId);
        if (doctor is null || user is null)
        {
            return false;
        }
        Doctor = doctor;
        DoctorUser = user;
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
