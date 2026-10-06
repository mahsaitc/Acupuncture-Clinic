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

    public async Task OnGetAsync()
    {
        Services = await db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id).ToListAsync();
        Content = await siteContent.GetAsync();
    }
}
