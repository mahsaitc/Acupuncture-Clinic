using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Records;

/// <summary>Streams a patient's file to a doctor. Opening a file is audited; thumbnails on a page already audited are not.</summary>
[Authorize(Policy = Policies.Doctor)]
public class FileModel(ClinicDbContext db, PrivateFileStore store, AuditLog audit) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, bool thumb = false, bool download = false)
    {
        var file = await db.MedicalFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (file is null || (thumb && !file.IsImage))
        {
            return NotFound();
        }
        var stream = store.Open(file.StoredName);
        if (stream is null)
        {
            return NotFound();
        }
        if (!thumb)
        {
            await audit.WriteAsync(AuditAction.ViewFile, file.PatientUserId, file.Id);
        }
        return FileResults.Private(Response, stream, file, download);
    }
}
