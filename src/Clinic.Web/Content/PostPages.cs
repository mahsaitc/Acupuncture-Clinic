using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Content;

/// <summary>Public list of published posts of one kind. English pages list only posts that have an English version.</summary>
public abstract class PostListPageModel(ClinicDbContext db) : PageModel
{
    public const int PageSize = 12;

    public abstract PostKind Kind { get; }

    [BindProperty(SupportsGet = true, Name = PageQueryKey)]
    public int PageNumber { get; set; } = 1;

    private const string PageQueryKey = "p";

    public List<Post> Posts { get; private set; } = [];
    public int PageCount { get; private set; } = 1;

    public async Task OnGetAsync()
    {
        var query = Published(db, Kind);
        PageCount = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, PageCount);
        Posts = await query
            .OrderByDescending(p => p.PublishedUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    internal static IQueryable<Post> Published(ClinicDbContext db, PostKind kind)
    {
        var query = db.Posts.AsNoTracking().Where(p => p.Kind == kind && p.IsPublished);
        return CulturePath.IsEnglish ? query.Where(p => p.TitleEn != null && p.BodyEn != null) : query;
    }
}

/// <summary>Public page for one published post.</summary>
public abstract class PostPageModel(ClinicDbContext db) : PageModel
{
    public abstract PostKind Kind { get; }

    public Post Post { get; private set; } = default!;
    public string AuthorName { get; private set; } = "";
    public List<Post> Related { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var post = await PostListPageModel.Published(db, Kind).FirstOrDefaultAsync(p => p.Slug == slug);
        if (post is null)
        {
            return NotFound();
        }

        Post = post;
        AuthorName = await db.Users.Where(u => u.Id == post.AuthorUserId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "";
        Related = await PostListPageModel.Published(db, Kind)
            .Where(p => p.Id != post.Id)
            .OrderByDescending(p => p.PublishedUtc)
            .Take(3)
            .ToListAsync();
        return Page();
    }
}
