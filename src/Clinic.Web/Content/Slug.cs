using System.Text;

namespace Clinic.Web.Content;

public static class Slug
{
    /// <summary>
    /// Builds a URL slug. Persian letters are kept (search engines index Persian URLs);
    /// spaces and punctuation become single hyphens.
    /// </summary>
    public static string From(string text, int maxLength = 100)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length > maxLength ? slug[..maxLength].TrimEnd('-') : slug;
    }
}
