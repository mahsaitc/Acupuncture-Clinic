using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Clinic.Domain.Entities;
using Clinic.Web.Branding;

namespace Clinic.Web.Seo;

/// <summary>Schema.org data (JSON-LD) that lets search engines show the clinic, its address and its articles richly.</summary>
public static class StructuredData
{
    private static readonly JsonSerializerOptions Json = new()
    {
        // Keeps Persian readable while still escaping < > & so the JSON cannot close its script tag.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(object data) => JsonSerializer.Serialize(data, Json);

    public sealed record Doctor(string Name, string? Specialty);

    public static object Clinic(HttpContext context, SiteContent site, Brand brand, bool english, IEnumerable<string> services, IEnumerable<Doctor> doctors)
    {
        var url = SiteUrl.Current(context, english, "/");
        var sameAs = new[] { site.InstagramUrl, site.TelegramUrl, site.YouTubeUrl, site.WhatsAppUrl }
            .Where(u => !string.IsNullOrWhiteSpace(u) && u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var serviceList = services.ToArray();
        var doctorList = doctors.ToArray();
        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "MedicalClinic",
            ["@id"] = SiteUrl.Persian(context, "/") + "#clinic",
            ["name"] = brand.Name,
            ["url"] = url,
            ["logo"] = SiteUrl.Absolute(context, brand.LogoPath),
            ["image"] = SiteUrl.Absolute(context, site.HeroPosterPath ?? brand.LogoPath),
            ["description"] = english ? site.HeroSubtitleEn : site.HeroSubtitleFa,
            ["telephone"] = Blank(site.Phone),
            ["email"] = Blank(site.Email),
            ["address"] = site.Address(english) is string address
                ? new Dictionary<string, object?> { ["@type"] = "PostalAddress", ["streetAddress"] = address, ["addressCountry"] = "IR" }
                : null,
            ["hasMap"] = Blank(site.MapLinkUrl),
            ["sameAs"] = sameAs.Length > 0 ? sameAs : null,
            ["inLanguage"] = english ? "en" : "fa",
            ["availableService"] = serviceList.Length > 0
                ? serviceList.Select(s => new Dictionary<string, object?> { ["@type"] = "MedicalTherapy", ["name"] = s }).ToArray()
                : null,
            ["employee"] = doctorList.Length > 0
                ? doctorList.Select(d => new Dictionary<string, object?> { ["@type"] = "Physician", ["name"] = d.Name, ["description"] = Blank(d.Specialty) }).ToArray()
                : null,
        };
    }

    public static object[] Post(HttpContext context, Post post, Brand brand, bool english, string authorName, string listName)
    {
        var path = SiteUrl.PostPath(post);
        var url = SiteUrl.Current(context, english, path);
        var listUrl = SiteUrl.Current(context, english, post.Kind == PostKind.Article ? "/Articles" : "/Blog");
        var article = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = post.Kind == PostKind.Article ? "MedicalScholarlyArticle" : "BlogPosting",
            ["headline"] = post.Title(english),
            ["description"] = Blank(post.Summary(english)),
            ["image"] = post.CoverImagePath is string cover ? SiteUrl.Absolute(context, cover) : null,
            ["datePublished"] = post.PublishedUtc?.ToString("o"),
            ["dateModified"] = post.UpdatedUtc.ToString("o"),
            ["inLanguage"] = english ? "en" : "fa",
            ["mainEntityOfPage"] = url,
            ["author"] = string.IsNullOrWhiteSpace(authorName) ? null : new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = authorName },
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "MedicalOrganization",
                ["name"] = brand.Name,
                ["logo"] = new Dictionary<string, object?> { ["@type"] = "ImageObject", ["url"] = SiteUrl.Absolute(context, brand.LogoPath) },
            },
        };
        var breadcrumb = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = new object[]
            {
                Crumb(1, brand.Name, SiteUrl.Current(context, english, "/")),
                Crumb(2, listName, listUrl),
                Crumb(3, post.Title(english), url),
            },
        };
        return [article, breadcrumb];
    }

    private static Dictionary<string, object?> Crumb(int position, string name, string url) =>
        new() { ["@type"] = "ListItem", ["position"] = position, ["name"] = name, ["item"] = url };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
