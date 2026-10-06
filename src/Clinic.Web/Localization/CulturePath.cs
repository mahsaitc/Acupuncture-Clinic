using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Clinic.Web.Localization;

/// <summary>
/// Persian is served at the site root and English under /en. The /en prefix is moved into
/// PathBase, so every link the app generates on an English page keeps the prefix automatically.
/// /fa is accepted as an alias of the root.
/// </summary>
public static class CulturePath
{
    public const string Persian = "fa";
    public const string English = "en";
    public static readonly string[] Supported = [Persian, English];

    private const string ItemKey = "clinic.culture";

    public static IApplicationBuilder UseCulturePathPrefix(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var culture = Persian;
            foreach (var candidate in Supported)
            {
                var prefix = new PathString("/" + candidate);
                if (context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase, out var rest))
                {
                    culture = candidate;
                    if (candidate == English)
                    {
                        context.Request.PathBase = context.Request.PathBase.Add(prefix);
                    }
                    context.Request.Path = rest.HasValue ? rest : "/";
                    break;
                }
            }
            context.Items[ItemKey] = culture;
            await next();
        });

    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == English;

    /// <summary>URL of the current page in the other language.</summary>
    public static string SwitchUrl(HttpRequest request)
    {
        var basePath = request.PathBase.Value ?? "";
        if (basePath.EndsWith("/" + English, StringComparison.OrdinalIgnoreCase))
        {
            basePath = basePath[..^(English.Length + 1)];
        }
        var target = IsEnglish ? basePath : basePath + "/" + English;
        var path = request.Path.Value == "/" && !IsEnglish ? "" : request.Path.Value;
        return (target + path + request.QueryString.Value) is { Length: > 0 } url ? url : "/";
    }

    public sealed class Provider : RequestCultureProvider
    {
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) =>
            Task.FromResult<ProviderCultureResult?>(
                httpContext.Items[ItemKey] is string culture ? new ProviderCultureResult(culture) : null);
    }
}
