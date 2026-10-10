using Clinic.Domain.Entities;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Branding;

/// <summary>The clinic's name, logo and colours for the current language, with the built-in defaults filled in.</summary>
public record Brand(string Name, string Title, string Subtitle, string Eyebrow, string LogoPath, bool CustomLogo, string? ThemeCss, string MetaColor)
{
    public const string DefaultLogo = "/img/logo.png";

    public static Brand From(SiteContent site, bool english, IStringLocalizer l) => new(
        Pick(english ? site.ClinicNameEn : site.ClinicNameFa) ?? l["Dr. Mahsa Hassani Acupuncture Clinic"],
        Pick(english ? site.BrandTitleEn : site.BrandTitleFa) ?? l["Acupuncture Clinic"],
        Pick(english ? site.BrandSubtitleEn : site.BrandSubtitleFa) ?? l["Dr. Mahsa Hassani"],
        Pick(english ? site.HeroEyebrowEn : site.HeroEyebrowFa) ?? l["Acupuncture and integrative medicine clinic"],
        site.LogoPath ?? DefaultLogo,
        site.LogoPath is not null,
        ThemePalette.Css(site.ThemeColor, site.AccentColor),
        ThemePalette.MetaColor(site.ThemeColor));

    private static string? Pick(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public static class LocalizerCultureExtensions
{
    /// <summary>The text of <paramref name="key"/> in another language than the current one, e.g. the Persian default on an English page.</summary>
    public static string WithCulture(this IStringLocalizer l, string culture, string key)
    {
        var current = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);
            return l[key];
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = current;
        }
    }
}
