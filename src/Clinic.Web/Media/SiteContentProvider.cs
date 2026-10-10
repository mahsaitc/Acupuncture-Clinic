using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Branding;
using Clinic.Web.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Media;

/// <summary>Loads the site content once per request for the layout and the home page.</summary>
public class SiteContentProvider(ClinicDbContext db, IStringLocalizer<SharedResource> l)
{
    private SiteContent? _content;

    public async Task<SiteContent> GetAsync() =>
        _content ??= await db.SiteContent.AsNoTracking().FirstOrDefaultAsync(c => c.Id == SiteContent.SingletonId)
                     ?? new SiteContent();

    /// <summary>The clinic's name, logo and colours in the current language.</summary>
    public async Task<Brand> BrandAsync() => Brand.From(await GetAsync(), CulturePath.IsEnglish, l);
}
