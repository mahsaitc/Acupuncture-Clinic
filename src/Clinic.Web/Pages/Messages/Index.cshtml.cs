using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Messages;

/// <summary>A signed-in user's own messages to the clinic, with the clinic's replies; they can answer back.</summary>
public class IndexModel(ClinicDbContext db, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    public List<ContactMessage> Messages { get; private set; } = [];
    public Dictionary<string, string> AuthorNames { get; private set; } = [];

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task OnGetAsync()
    {
        var userId = UserId;
        Messages = await db.ContactMessages.AsNoTracking()
            .Include(m => m.Replies.OrderBy(r => r.CreatedUtc))
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Replies.Max(r => (DateTime?)r.CreatedUtc) ?? m.CreatedUtc)
            .Take(100)
            .ToListAsync();
        var authors = Messages.SelectMany(m => m.Replies).Select(r => r.AuthorUserId).Distinct().ToList();
        AuthorNames = await db.Users.Where(u => authors.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    public async Task<IActionResult> OnPostReplyAsync(int id, string? body)
    {
        var userId = UserId;
        var message = await db.ContactMessages.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
        var text = body?.Trim();
        if (message is null)
        {
            return NotFound();
        }
        if (!string.IsNullOrEmpty(text))
        {
            db.MessageReplies.Add(new MessageReply
            {
                ContactMessageId = message.Id,
                AuthorUserId = userId,
                FromClinic = false,
                Body = text.Length > Admin.MessagesModel.MaxReplyLength ? text[..Admin.MessagesModel.MaxReplyLength] : text,
                CreatedUtc = time.GetUtcNow().UtcDateTime,
            });
            // The answer brings the message back to the top of the clinic's inbox as unread.
            message.ReadUtc = null;
            message.IsArchived = false;
            await db.SaveChangesAsync();
            TempData["Message"] = l["Your reply was sent."].Value;
        }
        return RedirectToPage(null, null, null, $"m{id}");
    }
}
