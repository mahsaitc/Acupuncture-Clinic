using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Posts;

[Authorize(Policy = Policies.Content)]
public class IndexModel(ClinicDbContext db, MediaStore media, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public PostKind Kind { get; set; } = PostKind.Blog;

    public List<Row> Rows { get; private set; } = [];

    public record Row(int Id, string Slug, string TitleFa, bool HasEnglish, bool IsPublished, DateTime? PublishedUtc, DateTime UpdatedUtc, string Author, string AuthorUserId);

    /// <summary>Only the admin edits, publishes and deletes; a doctor writes posts and sees their own.</summary>
    public bool IsAdmin => User.IsInRole(Clinic.Domain.Roles.Admin);

    public string? UserId => User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

    public async Task OnGetAsync()
    {
        var userId = UserId;
        Rows = await (
                from p in db.Posts.AsNoTracking()
                join u in db.Users on p.AuthorUserId equals u.Id
                where p.Kind == Kind && (IsAdmin || p.AuthorUserId == userId)
                orderby p.UpdatedUtc descending
                select new Row(p.Id, p.Slug, p.TitleFa, p.TitleEn != null && p.BodyEn != null, p.IsPublished, p.PublishedUtc, p.UpdatedUtc, u.FullName, p.AuthorUserId))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostPublishAsync(int id, bool publish)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.Kind == Kind);
        if (post is not null)
        {
            post.IsPublished = publish;
            post.PublishedUtc ??= publish ? time.GetUtcNow().UtcDateTime : null;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Kind });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.Kind == Kind);
        if (post is not null)
        {
            db.Posts.Remove(post);
            await db.SaveChangesAsync();
            media.Delete(post.CoverImagePath);
            TempData["Message"] = l["The post was deleted."].Value;
        }
        return RedirectToPage(new { Kind });
    }
}
