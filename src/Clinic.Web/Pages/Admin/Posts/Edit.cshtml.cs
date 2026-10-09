using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Content;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Posts;

[Authorize(Policy = Policies.Content)]
public class EditModel(ClinicDbContext db, MediaStore media, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public PostKind Kind { get; set; } = PostKind.Blog;

    [BindProperty]
    public PostInput Input { get; set; } = new();

    public string? CoverImagePath { get; private set; }

    /// <summary>A doctor only writes new posts; the admin edits, publishes and deletes them.</summary>
    public bool IsAdmin => User.IsInRole(Clinic.Domain.Roles.Admin);

    public class PostInput
    {
        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(250)]
        [Display(Name = "Title (Persian)")]
        public string TitleFa { get; set; } = "";

        [StringLength(600)]
        [Display(Name = "Summary (Persian)")]
        public string? SummaryFa { get; set; }

        [Required(ErrorMessage = "{0} is required.")]
        [Display(Name = "Text (Persian)")]
        public string BodyFa { get; set; } = "";

        [StringLength(250)]
        [Display(Name = "Title (English)")]
        public string? TitleEn { get; set; }

        [StringLength(600)]
        [Display(Name = "Summary (English)")]
        public string? SummaryEn { get; set; }

        [Display(Name = "Text (English)")]
        public string? BodyEn { get; set; }

        [StringLength(150)]
        [Display(Name = "Web address (slug)")]
        public string? Slug { get; set; }

        [StringLength(4000)]
        [Display(Name = "References")]
        public string? References { get; set; }

        [Display(Name = "Publish on the site")]
        public bool IsPublished { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id is not null && !IsAdmin)
        {
            TempData["Message"] = l["Only the clinic admin can change a post once it is sent."].Value;
            return RedirectToPage("./Index", new { kind = Kind });
        }
        if (Id is null)
        {
            return Page();
        }

        var post = await db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == Id && p.Kind == Kind);
        if (post is null)
        {
            return NotFound();
        }

        CoverImagePath = post.CoverImagePath;
        Input = new PostInput
        {
            TitleFa = post.TitleFa,
            SummaryFa = post.SummaryFa,
            BodyFa = post.BodyFa,
            TitleEn = post.TitleEn,
            SummaryEn = post.SummaryEn,
            BodyEn = post.BodyEn,
            Slug = post.Slug,
            References = post.References,
            IsPublished = post.IsPublished,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? cover, bool removeCover)
    {
        if (Id is not null && !IsAdmin)
        {
            return Forbid();
        }
        if (!IsAdmin)
        {
            // The admin decides whether a doctor's post goes on the site.
            Input.IsPublished = false;
        }
        Post? post = null;
        if (Id is not null)
        {
            post = await db.Posts.FirstOrDefaultAsync(p => p.Id == Id && p.Kind == Kind);
            if (post is null)
            {
                return NotFound();
            }
            CoverImagePath = post.CoverImagePath;
        }

        var slug = Clinic.Web.Content.Slug.From(string.IsNullOrWhiteSpace(Input.Slug) ? (Input.TitleEn is { Length: > 0 } en ? en : Input.TitleFa) : Input.Slug);
        if (slug.Length == 0)
        {
            ModelState.AddModelError("Input.Slug", l["Enter a web address using letters, digits and hyphens."]);
        }
        else if (await db.Posts.AnyAsync(p => p.Kind == Kind && p.Slug == slug && p.Id != Id))
        {
            ModelState.AddModelError("Input.Slug", l["Another post already uses this web address."]);
        }
        if (string.IsNullOrWhiteSpace(Input.TitleEn) != string.IsNullOrWhiteSpace(Input.BodyEn))
        {
            ModelState.AddModelError(string.Empty, l["For the English version, fill in both the English title and the English text."]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        string? newCover = null;
        if (cover is { Length: > 0 })
        {
            var saved = await media.SavePostImageAsync(cover);
            if (saved.Error != MediaStore.SaveError.None)
            {
                ModelState.AddModelError(string.Empty, l["The cover image must be a JPG, PNG or WebP file of at most 5 MB."]);
                return Page();
            }
            newCover = saved.WebPath;
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (post is null)
        {
            post = new Post
            {
                Kind = Kind,
                AuthorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                CreatedUtc = now,
            };
            db.Posts.Add(post);
        }

        post.Slug = slug;
        post.TitleFa = Input.TitleFa.Trim();
        post.SummaryFa = Clean(Input.SummaryFa);
        post.BodyFa = Input.BodyFa;
        post.TitleEn = Clean(Input.TitleEn);
        post.SummaryEn = Clean(Input.SummaryEn);
        post.BodyEn = Clean(Input.BodyEn);
        post.References = Clean(Input.References);
        post.IsPublished = Input.IsPublished;
        if (post.IsPublished)
        {
            post.PublishedUtc ??= now;
        }
        post.UpdatedUtc = now;

        var oldCover = post.CoverImagePath;
        if (newCover is not null)
        {
            post.CoverImagePath = newCover;
        }
        else if (removeCover)
        {
            post.CoverImagePath = null;
        }

        await db.SaveChangesAsync();
        if (oldCover != post.CoverImagePath)
        {
            media.Delete(oldCover);
        }

        if (!IsAdmin)
        {
            TempData["Message"] = l["Your post was sent to the clinic admin, who will publish it if approved."].Value;
            return RedirectToPage("./Index", new { kind = Kind });
        }
        TempData["Message"] = l["The post was saved."].Value;
        return RedirectToPage("./Edit", new { id = post.Id, kind = Kind });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
