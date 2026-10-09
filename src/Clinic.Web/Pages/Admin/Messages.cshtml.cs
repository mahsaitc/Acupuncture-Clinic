using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

/// <summary>
/// Inbox for the contact form and for notes left with appointments. Each doctor sees the messages sent to them;
/// the admin sees every message and can filter by doctor.
/// </summary>
public class MessagesModel(ClinicDbContext db, StaffScope scope, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool Archived { get; set; }

    /// <summary>Admin filter: a doctor's id, 0 for general messages, or null for all.</summary>
    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    public List<ContactMessage> Messages { get; private set; } = [];
    public List<(int Id, string Name)> Doctors { get; private set; } = [];
    public bool CanFilter => scope.IsAdmin;

    public string? DoctorName(int? id) => id is int d ? Doctors.FirstOrDefault(x => x.Id == d).Name : null;

    public async Task OnGetAsync()
    {
        Doctors = await scope.DoctorsAsync();
        var query = (await scope.MessagesAsync(db.ContactMessages)).Where(m => m.IsArchived == Archived);
        if (CanFilter && DoctorId is int doctorId)
        {
            query = doctorId == 0 ? query.Where(m => m.DoctorProfileId == null) : query.Where(m => m.DoctorProfileId == doctorId);
        }
        Messages = await query.AsNoTracking()
            .OrderByDescending(m => m.CreatedUtc)
            .Take(200)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostReadAsync(int id, bool read)
    {
        var message = await FindAsync(id);
        if (message is not null)
        {
            message.ReadUtc = read ? time.GetUtcNow().UtcDateTime : null;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Archived, DoctorId });
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id, bool archive)
    {
        var message = await FindAsync(id);
        if (message is not null)
        {
            message.IsArchived = archive;
            message.ReadUtc ??= time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Archived, DoctorId });
    }

    private async Task<ContactMessage?> FindAsync(int id) =>
        await (await scope.MessagesAsync(db.ContactMessages)).FirstOrDefaultAsync(m => m.Id == id);
}
