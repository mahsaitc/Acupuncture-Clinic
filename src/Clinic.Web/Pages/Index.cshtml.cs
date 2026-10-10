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

    /// <summary>Approved doctors, for the clinic's search engine data.</summary>
    public List<Clinic.Web.Seo.StructuredData.Doctor> Doctors { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Services = await db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id).ToListAsync();
        Content = await siteContent.GetAsync();
        Results = Content.Results();
        LatestPosts = await Clinic.Web.Content.PostListPageModel.Published(db, PostKind.Blog)
            .OrderByDescending(p => p.PublishedUtc)
            .Take(3)
            .ToListAsync();
        var english = Clinic.Web.Localization.CulturePath.IsEnglish;
        Doctors = (await db.Doctors.AsNoTracking()
                .Where(d => d.IsApproved)
                .Join(db.Users.Where(u => u.IsActive), d => d.UserId, u => u.Id, (d, u) => new { u.FullName, d.SpecialtyFa, d.SpecialtyEn })
                .OrderBy(d => d.FullName)
                .ToListAsync())
            .Select(d => new Clinic.Web.Seo.StructuredData.Doctor(d.FullName, english ? d.SpecialtyEn ?? d.SpecialtyFa : d.SpecialtyFa))
            .ToList();
    }
}
