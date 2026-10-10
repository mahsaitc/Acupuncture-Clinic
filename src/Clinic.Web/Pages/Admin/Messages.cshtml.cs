using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

/// <summary>
/// Inbox for the contact form and for notes left with appointments. Each doctor sees the messages sent to them;
/// the admin sees every message and can filter by doctor. Staff reply here; a signed-in patient reads the replies and
/// answers back on their own messages page.
/// </summary>
public class MessagesModel(ClinicDbContext db, StaffScope scope, TimeProvider time) : PageModel
{
    public const int MaxReplyLength = 4000;

    /// <summary>Names of everyone who wrote a reply on the page.</summary>
    public Dictionary<string, string> AuthorNames { get; private set; } = [];

    /// <summary>Doctors and the admin delete messages; reception only replies and archives.</summary>
    public bool CanDelete => scope.IsAdmin || User.IsInRole(Roles.Doctor);

    /// <summary>A clinic reply can be changed or removed by whoever wrote it, and by the admin.</summary>
    public bool CanChange(MessageReply reply) => reply.FromClinic && (scope.IsAdmin || reply.AuthorUserId == scope.UserId);

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
            .Include(m => m.Replies.OrderBy(r => r.CreatedUtc))
            .OrderByDescending(m => m.Replies.Max(r => (DateTime?)r.CreatedUtc) ?? m.CreatedUtc)
            .Take(200)
            .ToListAsync();
        var authors = Messages.SelectMany(m => m.Replies).Select(r => r.AuthorUserId).Distinct().ToList();
        AuthorNames = await db.Users.Where(u => authors.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    public async Task<IActionResult> OnPostReplyAsync(int id, string? body)
    {
        var message = await FindAsync(id);
        var text = Clean(body);
        if (message is not null && text is not null)
        {
            var now = time.GetUtcNow().UtcDateTime;
            db.MessageReplies.Add(new MessageReply { ContactMessageId = message.Id, AuthorUserId = scope.UserId!, FromClinic = true, Body = text, CreatedUtc = now });
            message.ReadUtc ??= now;
            await db.SaveChangesAsync();
        }
        return Back(id);
    }

    public async Task<IActionResult> OnPostEditReplyAsync(int replyId, string? body)
    {
        var reply = await FindReplyAsync(replyId);
        var text = Clean(body);
        if (reply is not null && text is not null)
        {
            reply.Body = text;
            reply.EditedUtc = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
        }
        return Back(reply?.ContactMessageId);
    }

    public async Task<IActionResult> OnPostDeleteReplyAsync(int replyId)
    {
        var reply = await FindReplyAsync(replyId);
        if (reply is not null)
        {
            db.MessageReplies.Remove(reply);
            await db.SaveChangesAsync();
        }
        return Back(reply?.ContactMessageId);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!CanDelete)
        {
            return Forbid();
        }
        var message = await FindAsync(id);
        if (message is not null)
        {
            db.ContactMessages.Remove(message);
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Archived, DoctorId });
    }

    /// <summary>A clinic reply on a message the user may see, if they may change it.</summary>
    private async Task<MessageReply?> FindReplyAsync(int replyId)
    {
        var reply = await db.MessageReplies.FirstOrDefaultAsync(r => r.Id == replyId);
        if (reply is null || !CanChange(reply) || await FindAsync(reply.ContactMessageId) is null)
        {
            return null;
        }
        return reply;
    }

    private RedirectToPageResult Back(int? messageId) =>
        RedirectToPage(null, null, new { Archived, DoctorId }, messageId is int m ? $"m{m}" : null);

    private static string? Clean(string? body)
    {
        var text = body?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > MaxReplyLength ? text[..MaxReplyLength] : text;
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
