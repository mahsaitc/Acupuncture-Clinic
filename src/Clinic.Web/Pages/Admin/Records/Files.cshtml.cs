using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Records;

[Authorize(Policy = Policies.Doctor)]
[RequestSizeLimit(PrivateFileStore.MaxBytes + 1024 * 1024)]
public class FilesModel(
    ClinicDbContext db,
    UserManager<ApplicationUser> users,
    PrivateFileStore store,
    AuditLog audit,
    TimeProvider time,
    IStringLocalizer<SharedResource> l) : PageModel
{
    public ApplicationUser Patient { get; private set; } = default!;
    public List<MedicalFile> Files { get; private set; } = [];
    public Dictionary<string, string> UploaderNames { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public FileCategory? Category { get; set; }

    [BindProperty]
    public UploadInput Upload { get; set; } = new();

    public class UploadInput
    {
        [Display(Name = "Category")]
        public FileCategory Category { get; set; }

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Title")]
        public string Title { get; set; } = "";

        [Display(Name = "Date taken")]
        public string? TakenOn { get; set; }

        [StringLength(1000)]
        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Display(Name = "Show to the patient")]
        public bool VisibleToPatient { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string patientId)
    {
        if (!await LoadAsync(patientId))
        {
            return NotFound();
        }
        await audit.WriteAsync(AuditAction.ViewRecord, patientId);
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(string patientId, IFormFile? file)
    {
        if (!await LoadAsync(patientId))
        {
            return NotFound();
        }

        DateOnly? takenOn = null;
        if (!string.IsNullOrWhiteSpace(Upload.TakenOn))
        {
            if (DisplayFormat.TryParseDateInput(Upload.TakenOn, out var parsed))
            {
                takenOn = parsed;
            }
            else
            {
                ModelState.AddModelError("Upload.TakenOn", l["Enter the date like {0}.", DisplayFormat.DateInputHint]);
            }
        }
        if (file is not { Length: > 0 })
        {
            ModelState.AddModelError(string.Empty, l["Choose a file to upload."]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var saved = await store.SaveAsync(file!);
        if (saved.Error != PrivateFileStore.SaveError.None)
        {
            ModelState.AddModelError(string.Empty, l["The file must be a JPG, PNG, WebP, PDF or DICOM file of at most 50 MB."]);
            return Page();
        }

        var record = new MedicalFile
        {
            PatientUserId = patientId,
            Category = Upload.Category,
            Title = Upload.Title.Trim(),
            Note = string.IsNullOrWhiteSpace(Upload.Note) ? null : Upload.Note.Trim(),
            StoredName = saved.StoredName!,
            ContentType = saved.ContentType!,
            SizeBytes = saved.Size,
            TakenOn = takenOn,
            UploadedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            UploadedUtc = time.GetUtcNow().UtcDateTime,
            VisibleToPatient = Upload.VisibleToPatient,
        };
        db.MedicalFiles.Add(record);
        await db.SaveChangesAsync();
        await audit.WriteAsync(AuditAction.UploadFile, patientId, record.Id);

        TempData["Message"] = l["The file was uploaded."].Value;
        return RedirectToPage(new { patientId });
    }

    public async Task<IActionResult> OnPostVisibilityAsync(string patientId, int id)
    {
        var file = await db.MedicalFiles.FirstOrDefaultAsync(f => f.Id == id && f.PatientUserId == patientId);
        if (file is null)
        {
            return NotFound();
        }
        file.VisibleToPatient = !file.VisibleToPatient;
        audit.Add(AuditAction.UpdateFile, patientId, id);
        await db.SaveChangesAsync();
        return RedirectToPage(new { patientId, category = Category });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string patientId, int id)
    {
        var file = await db.MedicalFiles.FirstOrDefaultAsync(f => f.Id == id && f.PatientUserId == patientId);
        if (file is null)
        {
            return NotFound();
        }
        db.MedicalFiles.Remove(file);
        audit.Add(AuditAction.DeleteFile, patientId, id);
        await db.SaveChangesAsync();
        store.Delete(file.StoredName);

        TempData["Message"] = l["The file was deleted."].Value;
        return RedirectToPage(new { patientId, category = Category });
    }

    private async Task<bool> LoadAsync(string patientId)
    {
        var patient = await RecordHeader.FindPatientAsync(users, patientId);
        if (patient is null)
        {
            return false;
        }
        Patient = patient;

        var query = db.MedicalFiles.AsNoTracking().Where(f => f.PatientUserId == patientId);
        if (Category is FileCategory category)
        {
            query = query.Where(f => f.Category == category);
        }
        Files = await query.OrderByDescending(f => f.UploadedUtc).ToListAsync();
        var uploaderIds = Files.Select(f => f.UploadedByUserId).Distinct().ToList();
        UploaderNames = await db.Users.Where(u => uploaderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return true;
    }
}
