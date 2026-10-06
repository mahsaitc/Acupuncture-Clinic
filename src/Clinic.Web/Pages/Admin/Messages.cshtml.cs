using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

/// <summary>Inbox for the public contact form.</summary>
public class MessagesModel(ClinicDbContext db, TimeProvider time) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool Archived { get; set; }

    public List<ContactMessage> Messages { get; private set; } = [];

    public async Task OnGetAsync() =>
        Messages = await db.ContactMessages.AsNoTracking()
            .Where(m => m.IsArchived == Archived)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(200)
            .ToListAsync();

    public async Task<IActionResult> OnPostReadAsync(int id, bool read)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is not null)
        {
            message.ReadUtc = read ? time.GetUtcNow().UtcDateTime : null;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Archived });
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id, bool archive)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is not null)
        {
            message.IsArchived = archive;
            message.ReadUtc ??= time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Archived });
    }
}
