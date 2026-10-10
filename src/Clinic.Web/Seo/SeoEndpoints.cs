using System.Globalization;
using System.Text;
using System.Xml;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Seo;

/// <summary>/robots.txt and /sitemap.xml, built from the published content so new posts are found without any manual step.</summary>
public static class SeoEndpoints
{
    /// <summary>Public pages listed in the sitemap besides the posts.</summary>
    private static readonly string[] PublicPages = ["/", "/Contact", "/Blog", "/Articles"];

    public static void MapClinicSeo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", (HttpContext context) =>
        {
            var text = new StringBuilder("User-agent: *\n");
            foreach (var path in SecurityHeaders.PrivatePaths)
            {
                text.Append("Disallow: ").Append(path).Append('\n');
                text.Append("Disallow: /en").Append(path).Append('\n');
            }
            text.Append("\nSitemap: ").Append(SiteUrl.Origin(context)).Append("/sitemap.xml\n");
            return Results.Text(text.ToString(), "text/plain; charset=utf-8");
        });

        app.MapGet("/sitemap.xml", async (HttpContext context, ClinicDbContext db) =>
        {
            var site = await db.SiteContent.AsNoTracking().FirstOrDefaultAsync(c => c.Id == SiteContent.SingletonId);
            var posts = await db.Posts.AsNoTracking()
                .Where(p => p.IsPublished)
                .OrderByDescending(p => p.PublishedUtc)
                .ToListAsync();

            var output = new StringBuilder();
            using (var xml = XmlWriter.Create(output, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
            {
                xml.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                xml.WriteAttributeString("xmlns", "xhtml", null, "http://www.w3.org/1999/xhtml");
                foreach (var path in PublicPages)
                {
                    var modified = path == "/" ? site?.UpdatedUtc : null;
                    Write(xml, context, path, english: false, hasEnglish: true, modified);
                    Write(xml, context, path, english: true, hasEnglish: true, modified);
                }
                foreach (var post in posts)
                {
                    var path = SiteUrl.PostPath(post);
                    Write(xml, context, path, english: false, post.HasEnglish, post.UpdatedUtc);
                    if (post.HasEnglish)
                    {
                        Write(xml, context, path, english: true, hasEnglish: true, post.UpdatedUtc);
                    }
                }
                xml.WriteEndElement();
            }
            // StringBuilder output is labelled UTF-16 by XmlWriter; the response is UTF-8.
            return Results.Text(output.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\""), "application/xml; charset=utf-8");
        });
    }

    private static void Write(XmlWriter xml, HttpContext context, string path, bool english, bool hasEnglish, DateTime? modified)
    {
        xml.WriteStartElement("url");
        xml.WriteElementString("loc", SiteUrl.Current(context, english, path));
        if (modified is DateTime m)
        {
            xml.WriteElementString("lastmod", m.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (hasEnglish)
        {
            Alternate(xml, "fa", SiteUrl.Persian(context, path));
            Alternate(xml, "en", SiteUrl.English(context, path));
            Alternate(xml, "x-default", SiteUrl.Persian(context, path));
        }
        xml.WriteEndElement();
    }

    private static void Alternate(XmlWriter xml, string language, string href)
    {
        xml.WriteStartElement("xhtml", "link", "http://www.w3.org/1999/xhtml");
        xml.WriteAttributeString("rel", "alternate");
        xml.WriteAttributeString("hreflang", language);
        xml.WriteAttributeString("href", href);
        xml.WriteEndElement();
    }
}
