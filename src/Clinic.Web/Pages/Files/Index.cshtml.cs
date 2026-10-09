using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Files;

/// <summary>A patient's own files: what the doctor shared, and lab or radiology results they upload themselves.</summary>
[RequestSizeLimit(PrivateFileStore.MaxBytes + 1024 * 1024)]
public class IndexModel(ClinicDbContext db, PrivateFileStore store, AuditLog audit, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    /// <summary>What a patient may upload. Clinical photos are taken by the clinic.</summary>
    public static readonly FileCategory[] PatientCategories = [FileCategory.Lab, FileCategory.Radiology, FileCategory.Other];

    public List<MedicalFile> Files { get; private set; } = [];

    [BindProperty]
    public UploadInput Upload { get; set; } = new();

    public class UploadInput
    {
        [Display(Name = "Category")]
        public FileCategory Category { get; set; } = FileCategory.Lab;

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Title")]
        public string Title { get; set; } = "";

        [Display(Name = "Date taken")]
        public string? TakenOn { get; set; }

        [StringLength(1000)]
        [Display(Name = "Note for the doctor")]
        public string? Note { get; set; }
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync(IFormFile? file)
    {
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
        if (!PatientCategories.Contains(Upload.Category))
        {
            ModelState.AddModelError("Upload.Category", l["Choose a category."]);
        }
        if (file is not { Length: > 0 })
        {
            ModelState.AddModelError(string.Empty, l["Choose a file to upload."]);
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var saved = await store.SaveAsync(file!);
        if (saved.Error != PrivateFileStore.SaveError.None)
        {
            ModelState.AddModelError(string.Empty, l["The file must be a JPG, PNG, WebP, PDF or DICOM file of at most 200 KB."]);
            await LoadAsync();
            return Page();
        }

        var record = new MedicalFile
        {
            PatientUserId = UserId,
            Category = Upload.Category,
            Title = Upload.Title.Trim(),
            Note = string.IsNullOrWhiteSpace(Upload.Note) ? null : Upload.Note.Trim(),
            StoredName = saved.StoredName!,
            ContentType = saved.ContentType!,
            SizeBytes = saved.Size,
            TakenOn = takenOn,
            UploadedByUserId = UserId,
            UploadedUtc = time.GetUtcNow().UtcDateTime,
            VisibleToPatient = true,
        };
        db.MedicalFiles.Add(record);
        await db.SaveChangesAsync();
        await audit.WriteAsync(AuditAction.UploadFile, UserId, record.Id);

        TempData["Message"] = l["The file was uploaded. The doctor will see it in your record."].Value;
        return RedirectToPage();
    }

    /// <summary>Streams one of the patient's own files.</summary>
    public async Task<IActionResult> OnGetOpenAsync(int id, bool download = false)
    {
        var file = await db.MedicalFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.PatientUserId == UserId && (f.VisibleToPatient || f.UploadedByUserId == UserId));
        var stream = file is null ? null : store.Open(file.StoredName);
        if (stream is null)
        {
            return NotFound();
        }
        await audit.WriteAsync(AuditAction.ViewFile, UserId, file!.Id);
        return FileResults.Private(Response, stream, file, download);
    }

    private async Task LoadAsync()
    {
        Files = await db.MedicalFiles.AsNoTracking()
            .Where(f => f.PatientUserId == UserId && (f.VisibleToPatient || f.UploadedByUserId == UserId))
            .OrderByDescending(f => f.UploadedUtc)
            .ToListAsync();
    }
}
