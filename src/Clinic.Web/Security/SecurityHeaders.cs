using System.Security.Cryptography;

namespace Clinic.Web.Security;

/// <summary>
/// Browser-side protection on every response: a Content Security Policy, no framing by other sites,
/// no MIME sniffing, a strict referrer policy, and "noindex" for the private parts of the site.
/// Everything the site loads is self-hosted, so the policy allows only this site, plus the Google Maps
/// embed on the contact page. Inline scripts run only with the per-request nonce (see <see cref="NonceTagHelper"/>).
/// </summary>
public static class SecurityHeaders
{
    private const string NonceKey = "clinic.csp-nonce";

    /// <summary>Paths that hold personal or staff-only pages; search engines are told not to index them.</summary>
    public static readonly string[] PrivatePaths = ["/Admin", "/Account", "/Files", "/Messages", "/Appointments", "/Booking"];

    public static string CspNonce(this HttpContext context)
    {
        if (context.Items[NonceKey] is not string nonce)
        {
            nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;
        }
        return nonce;
    }

    public static bool IsPrivatePath(PathString path) =>
        PrivatePaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool upgradeInsecureRequests) =>
        app.Use(async (context, next) =>
        {
            var nonce = context.CspNonce();
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = string.Join("; ",
                    "default-src 'self'",
                    $"script-src 'self' 'nonce-{nonce}'",
                    // Bootstrap and the pages use style attributes; styles cannot run code.
                    "style-src 'self' 'unsafe-inline'",
                    // Post bodies may show images from other https sites; Bootstrap uses data: icons.
                    "img-src 'self' data: blob: https:",
                    "font-src 'self' data:",
                    "media-src 'self' blob:",
                    "frame-src https://www.google.com",
                    "connect-src 'self'",
                    "object-src 'none'",
                    "base-uri 'self'",
                    "form-action 'self'",
                    "frame-ancestors 'self'"
                    + (upgradeInsecureRequests ? "; upgrade-insecure-requests" : ""));
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "SAMEORIGIN";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                if (IsPrivatePath(context.Request.Path))
                {
                    headers["X-Robots-Tag"] = "noindex, nofollow";
                    // Pages with medical or personal data must not be kept by shared caches or the back button cache.
                    headers.CacheControl = "no-store";
                }
                return Task.CompletedTask;
            });
            await next();
        });
}
