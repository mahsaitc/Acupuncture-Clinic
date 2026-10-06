using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Media;

/// <summary>Loads the site content once per request for the layout and the home page.</summary>
public class SiteContentProvider(ClinicDbContext db)
{
    private SiteContent? _content;

    public async Task<SiteContent> GetAsync() =>
        _content ??= await db.SiteContent.AsNoTracking().FirstOrDefaultAsync(c => c.Id == SiteContent.SingletonId)
                     ?? new SiteContent();
}
