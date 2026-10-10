using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Clinic.Web.Branding;

/// <summary>
/// Turns the owner's two chosen colours into the site's colour tokens. Every olive shade keeps its place on the
/// light-dark scale relative to olive-600, so a new colour gets the same contrast as the original theme.
/// </summary>
public static partial class ThemePalette
{
    public const string DefaultTheme = "#5d7139";
    public const string DefaultAccent = "#c2a568";

    private static readonly (string Name, string Hex)[] Olive =
    [
        ("olive-950", "#1f2714"), ("olive-900", "#2c371c"), ("olive-800", "#3b4a25"), ("olive-700", "#4b5d2e"),
        ("olive-600", "#5d7139"), ("olive-500", "#74874a"), ("olive-300", "#b5c08f"), ("olive-100", "#eef1e3"),
        ("olive-50", "#f6f7f0"),
    ];

    private static readonly (string Name, string Hex)[] Sand = [("sand-500", "#c2a568"), ("sand-300", "#e3d3a8")];

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    public static bool IsValid(string? hex) => hex is not null && HexColor().IsMatch(hex);

    /// <summary>A normalised #rrggbb, or null for an invalid value or the default colour.</summary>
    public static string? Normalize(string? hex, string defaultHex)
    {
        hex = hex?.Trim().ToLowerInvariant();
        return IsValid(hex) && hex != defaultHex ? hex : null;
    }

    /// <summary>The token overrides for <c>:root</c>, or null when both colours are the defaults.</summary>
    public static string? Css(string? theme, string? accent)
    {
        theme = Normalize(theme, DefaultTheme);
        accent = Normalize(accent, DefaultAccent);
        if (theme is null && accent is null)
        {
            return null;
        }

        var css = new StringBuilder(":root{");
        var tokens = new Dictionary<string, string>();
        if (theme is not null)
        {
            foreach (var (name, hex) in Olive)
            {
                tokens[name] = Shift(hex, DefaultTheme, theme);
            }
            var rgb700 = Rgb(tokens["olive-700"]);
            var rgb900 = Rgb(tokens["olive-900"]);
            var rgb600 = Rgb(tokens["olive-600"]);
            var rgb500 = Rgb(tokens["olive-500"]);
            css.Append(CultureInfo.InvariantCulture,
                $"--bs-link-color-rgb:{rgb700};--bs-link-hover-color-rgb:{rgb900};--bs-success-rgb:{rgb700};--bs-primary-rgb:{rgb600};" +
                $"--bs-focus-ring-color:rgba({rgb500},.35);--shadow-soft:0 10px 30px rgba({rgb900},.08);--shadow-lift:0 18px 40px rgba({rgb900},.16);");
        }
        if (accent is not null)
        {
            foreach (var (name, hex) in Sand)
            {
                tokens[name] = Shift(hex, DefaultAccent, accent);
            }
        }
        foreach (var (name, value) in tokens)
        {
            css.Append("--").Append(name).Append(':').Append(value).Append(';');
        }
        return css.Append('}').ToString();
    }

    /// <summary>The colour for the browser's address bar (the shade of olive-700).</summary>
    public static string MetaColor(string? theme) =>
        Normalize(theme, DefaultTheme) is { } t ? Shift("#4b5d2e", DefaultTheme, t) : "#4b5d2e";

    /// <summary>Moves <paramref name="shade"/> from the old base colour's family into the new one's.</summary>
    private static string Shift(string shade, string oldBase, string newBase)
    {
        var (_, ss, sl) = Hsl(shade);
        var (_, bs, bl) = Hsl(oldBase);
        var (nh, ns, nl) = Hsl(newBase);

        // Lightness: darker shades scale towards black, lighter ones towards white, from the new base's lightness.
        var l = sl <= bl ? sl * nl / bl : nl + (sl - bl) / (1 - bl) * (1 - nl);
        // Saturation follows the new base, keeping each shade's ratio to the old base.
        var s = bs == 0 ? ns : Math.Clamp(ss * ns / bs, 0, 1);
        return Hex(nh, s, Math.Clamp(l, 0, 1));
    }

    private static (double R, double G, double B) Parse(string hex) =>
        (Convert.ToInt32(hex[1..3], 16) / 255.0, Convert.ToInt32(hex[3..5], 16) / 255.0, Convert.ToInt32(hex[5..7], 16) / 255.0);

    private static string Rgb(string hex)
    {
        var (r, g, b) = Parse(hex);
        return $"{Math.Round(r * 255)}, {Math.Round(g * 255)}, {Math.Round(b * 255)}";
    }

    private static (double H, double S, double L) Hsl(string hex)
    {
        var (r, g, b) = Parse(hex);
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max == min)
        {
            return (0, 0, l);
        }
        var d = max - min;
        var s = l > .5 ? d / (2 - max - min) : d / (max + min);
        var h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static string Hex(double h, double s, double l)
    {
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < .5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Channel(p, q, h + 1.0 / 3);
            g = Channel(p, q, h);
            b = Channel(p, q, h - 1.0 / 3);
        }
        return $"#{To255(r):x2}{To255(g):x2}{To255(b):x2}";
    }

    private static int To255(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);

    private static double Channel(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < .5) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
