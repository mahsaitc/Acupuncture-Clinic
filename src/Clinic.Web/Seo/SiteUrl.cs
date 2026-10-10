namespace Clinic.Web.Seo;

/// <summary>
/// Absolute addresses for search engines and link previews. "Site:BaseUrl" (e.g. https://example.ir) wins over the
/// request's own host, which can be wrong behind a proxy. Paths are given without the /en prefix.
/// </summary>
public static class SiteUrl
{
    public static string Origin(HttpContext context)
    {
        var configured = context.RequestServices.GetRequiredService<IConfiguration>()["Site:BaseUrl"];
        return string.IsNullOrWhiteSpace(configured)
            ? $"{context.Request.Scheme}://{context.Request.Host}"
            : configured.Trim().TrimEnd('/');
    }

    public static string Persian(HttpContext context, string path) => Origin(context) + (string.IsNullOrEmpty(path) ? "/" : path);

    public static string English(HttpContext context, string path) => Origin(context) + "/en" + (path is "/" or "" ? "" : path);

    public static string Current(HttpContext context, bool english, string path) => english ? English(context, path) : Persian(context, path);

    /// <summary>A site path such as /media/brand/logo.png made absolute; full URLs are returned as they are.</summary>
    public static string Absolute(HttpContext context, string pathOrUrl) =>
        pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? pathOrUrl
            : Origin(context) + (pathOrUrl.StartsWith('/') ? pathOrUrl : "/" + pathOrUrl);

    /// <summary>The path of a post page, with the slug escaped.</summary>
    public static string PostPath(Clinic.Domain.Entities.Post post) =>
        (post.Kind == Clinic.Domain.Entities.PostKind.Article ? "/Articles/" : "/Blog/") + Uri.EscapeDataString(post.Slug);
}
