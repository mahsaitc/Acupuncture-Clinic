using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages;

public class IndexModel(ClinicDbContext db, SiteContentProvider siteContent) : PageModel
{
    public List<ClinicService> Services { get; private set; } = [];
    public SiteContent Content { get; private set; } = new();
    public List<Post> LatestPosts { get; private set; } = [];
    public ClinicResults Results { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Services = await db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id).ToListAsync();
        Content = await siteContent.GetAsync();
        Results = Content.Results();
        LatestPosts = await Clinic.Web.Content.PostListPageModel.Published(db, PostKind.Blog)
            .OrderByDescending(p => p.PublishedUtc)
            .Take(3)
            .ToListAsync();
    }
}
