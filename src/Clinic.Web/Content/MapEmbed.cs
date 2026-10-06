using System.Net;
using System.Text.RegularExpressions;

namespace Clinic.Web.Content;

/// <summary>Accepts either Google Maps' whole embed &lt;iframe&gt; code or just its src, and keeps only a Google Maps embed URL.</summary>
public static partial class MapEmbed
{
    private const string AllowedPrefix = "https://www.google.com/maps/embed?";

    public static string? ExtractSrc(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var value = input.Trim();
        var match = SrcAttribute().Match(value);
        if (match.Success)
        {
            value = WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        return value.StartsWith(AllowedPrefix, StringComparison.Ordinal)
               && Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && uri.Host == "www.google.com"
            ? value
            : null;
    }

    [GeneratedRegex("""src\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex SrcAttribute();
}
