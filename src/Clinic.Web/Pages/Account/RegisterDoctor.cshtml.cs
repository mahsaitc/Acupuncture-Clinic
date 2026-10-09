using System.ComponentModel.DataAnnotations;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Account;

/// <summary>
/// Doctor sign-up, separate from patients. The doctor uploads their medical licence and national ID card; the account
/// has no role and cannot sign in until the admin checks the documents and approves it.
/// </summary>
public class RegisterDoctorModel(
    UserManager<ApplicationUser> userManager,
    ClinicDbContext db,
    NationalCodeIndex nationalCodes,
    PrivateFileStore store,
    TimeProvider time,
    IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    [BindProperty]
    [Required(ErrorMessage = "{0} is required.")]
    [RegularExpression(@"^\s*[0-9۰-۹]{10}\s*$", ErrorMessage = "The national code must have exactly 10 digits.")]
    [Display(Name = "National code")]
    public string NationalCode { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "{0} is required.")]
    [RegularExpression(@"^\s*[0-9۰-۹]{3,10}\s*$", ErrorMessage = "Enter digits only.")]
    [Display(Name = "Medical Council number")]
    public string MedicalCouncilNumber { get; set; } = "";

    [BindProperty]
    [StringLength(100)]
    [Display(Name = "Specialty")]
    public string? Specialty { get; set; }

    [BindProperty]
    [Display(Name = "Medical licence or Medical Council card")]
    public IFormFile? MedicalLicense { get; set; }

    [BindProperty]
    [Display(Name = "National ID card")]
    public IFormFile? NationalIdCard { get; set; }

    [BindProperty]
    [Display(Name = "Other documents (optional)")]
    public List<IFormFile>? OtherDocuments { get; set; }

    /// <summary>At most this many optional documents.</summary>
    public const int MaxOtherDocuments = 3;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (MedicalLicense is null)
        {
            ModelState.AddModelError(nameof(MedicalLicense), l["{0} is required.", l["Medical licence or Medical Council card"]]);
        }
        if (NationalIdCard is null)
        {
            ModelState.AddModelError(nameof(NationalIdCard), l["{0} is required.", l["National ID card"]]);
        }
        if (OtherDocuments?.Count > MaxOtherDocuments)
        {
            ModelState.AddModelError(nameof(OtherDocuments), l["Upload at most {0} other documents.", MaxOtherDocuments]);
        }
        if (await nationalCodes.InUseAsync(NationalCode, exceptUserId: null))
        {
            ModelState.AddModelError(nameof(NationalCode), l["This national code is already registered for another person."]);
        }
        if (await CouncilNumbers.InUseAsync(db, MedicalCouncilNumber))
        {
            ModelState.AddModelError(nameof(MedicalCouncilNumber), l["This Medical Council number is already registered for another doctor."]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Files are checked and stored before the account exists, so a bad file leaves no half-made account.
        var uploads = new List<(DoctorDocumentKind Kind, IFormFile File)> { (DoctorDocumentKind.MedicalLicense, MedicalLicense!), (DoctorDocumentKind.NationalIdCard, NationalIdCard!) };
        uploads.AddRange((OtherDocuments ?? []).Where(f => f.Length > 0).Select(f => (DoctorDocumentKind.OtherDocument, f)));
        var now = time.GetUtcNow().UtcDateTime;
        var documents = new List<DoctorDocument>();
        foreach (var (kind, file) in uploads)
        {
            var saved = await store.SaveAsync(file, PrivateFileStore.DocumentMaxBytes);
            if (saved.Error != PrivateFileStore.SaveError.None)
            {
                documents.ForEach(d => store.Delete(d.StoredName));
                ModelState.AddModelError(string.Empty, saved.Error == PrivateFileStore.SaveError.TooLarge
                    ? l["{0} is larger than 2 MB.", file.FileName]
                    : l["{0} must be a JPG, PNG, WEBP or PDF file.", file.FileName]);
                return Page();
            }
            documents.Add(new DoctorDocument { Kind = kind, StoredName = saved.StoredName!, ContentType = saved.ContentType!, SizeBytes = saved.Size, UploadedUtc = now });
        }

        var user = await RegisterModel.CreateUserAsync(userManager, Input, ModelState, role: null, l, nationalCodes.Hash(NationalCode));
        if (user is null)
        {
            documents.ForEach(d => store.Delete(d.StoredName));
            return Page();
        }

        db.Doctors.Add(new DoctorProfile
        {
            UserId = user.Id,
            NationalCode = NationalCodeIndex.Normalize(NationalCode),
            MedicalCouncilNumber = CouncilNumbers.Normalize(MedicalCouncilNumber),
            SpecialtyFa = string.IsNullOrWhiteSpace(Specialty) ? null : Specialty.Trim(),
            SpecialtyEn = string.IsNullOrWhiteSpace(Specialty) ? null : Specialty.Trim(),
            IsApproved = false,
            RequestedUtc = now,
            Documents = documents,
        });
        await db.SaveChangesAsync();

        TempData["Message"] = l["Your request and documents were sent. You can sign in once the clinic admin approves your account."].Value;
        return RedirectToPage("./Login");
    }
}
