using System.ComponentModel.DataAnnotations;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Branding;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

/// <summary>The site owner sets the clinic's name, logo, home page title and theme colours, so one codebase serves any clinic.</summary>
[Authorize(Policy = Policies.Owner)]
[RequestSizeLimit(MediaStore.MaxImageBytes + 1024 * 1024)]
public class BrandingModel(ClinicDbContext db, MediaStore media, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public BrandingInput Input { get; set; } = new();

    public string LogoPath { get; private set; } = Brand.DefaultLogo;
    public bool CustomLogo { get; private set; }

    /// <summary>Ready-made colour pairs (theme, accent) the owner can start from.</summary>
    public static readonly (string Name, string Theme, string Accent)[] Presets =
    [
        ("Olive", ThemePalette.DefaultTheme, ThemePalette.DefaultAccent),
        ("Teal", "#2f7f7a", "#d9a441"),
        ("Blue", "#2f5f9e", "#d4a24c"),
        ("Plum", "#7a4a7e", "#c9a46a"),
        ("Terracotta", "#a5543a", "#d7b46a"),
        ("Slate", "#4a5868", "#c49a5a"),
    ];

    public class BrandingInput
    {
        [StringLength(150)]
        [Display(Name = "Clinic name (Persian)")]
        public string? ClinicNameFa { get; set; }

        [StringLength(150)]
        [Display(Name = "Clinic name (English)")]
        public string? ClinicNameEn { get; set; }

        [StringLength(80)]
        [Display(Name = "Menu title (Persian)")]
        public string? BrandTitleFa { get; set; }

        [StringLength(80)]
        [Display(Name = "Menu title (English)")]
        public string? BrandTitleEn { get; set; }

        [StringLength(80)]
        [Display(Name = "Line under the menu title (Persian)")]
        public string? BrandSubtitleFa { get; set; }

        [StringLength(80)]
        [Display(Name = "Line under the menu title (English)")]
        public string? BrandSubtitleEn { get; set; }

        [StringLength(100)]
        [Display(Name = "Label above the home page title (Persian)")]
        public string? HeroEyebrowFa { get; set; }

        [StringLength(100)]
        [Display(Name = "Label above the home page title (English)")]
        public string? HeroEyebrowEn { get; set; }

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Home page title (Persian)")]
        public string HeroTitleFa { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Home page title (English)")]
        public string HeroTitleEn { get; set; } = "";

        [Display(Name = "Theme colour")]
        public string ThemeColor { get; set; } = ThemePalette.DefaultTheme;

        [Display(Name = "Accent colour")]
        public string AccentColor { get; set; } = ThemePalette.DefaultAccent;
    }

    public async Task OnGetAsync()
    {
        var site = await LoadAsync();
        Input = new BrandingInput
        {
            ClinicNameFa = site.ClinicNameFa ?? l.WithCulture("fa", "Dr. Mahsa Hassani Acupuncture Clinic"),
            ClinicNameEn = site.ClinicNameEn ?? "Dr. Mahsa Hassani Acupuncture Clinic",
            BrandTitleFa = site.BrandTitleFa ?? l.WithCulture("fa", "Acupuncture Clinic"),
            BrandTitleEn = site.BrandTitleEn ?? "Acupuncture Clinic",
            BrandSubtitleFa = site.BrandSubtitleFa ?? l.WithCulture("fa", "Dr. Mahsa Hassani"),
            BrandSubtitleEn = site.BrandSubtitleEn ?? "Dr. Mahsa Hassani",
            HeroEyebrowFa = site.HeroEyebrowFa ?? l.WithCulture("fa", "Acupuncture and integrative medicine clinic"),
            HeroEyebrowEn = site.HeroEyebrowEn ?? "Acupuncture and integrative medicine clinic",
            HeroTitleFa = site.HeroTitleFa,
            HeroTitleEn = site.HeroTitleEn,
            ThemeColor = site.ThemeColor ?? ThemePalette.DefaultTheme,
            AccentColor = site.AccentColor ?? ThemePalette.DefaultAccent,
        };
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? logo, bool removeLogo)
    {
        var site = await LoadAsync();
        if (!ThemePalette.IsValid(Input.ThemeColor?.Trim()))
        {
            ModelState.AddModelError("Input.ThemeColor", l["Pick a colour."]);
        }
        if (!ThemePalette.IsValid(Input.AccentColor?.Trim()))
        {
            ModelState.AddModelError("Input.AccentColor", l["Pick a colour."]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (logo is { Length: > 0 })
        {
            var saved = await media.SaveBrandImageAsync(logo);
            if (saved.Error != MediaStore.SaveError.None)
            {
                ModelState.AddModelError(string.Empty, l["The logo must be a JPG, PNG or WebP file of at most 5 MB."]);
                return Page();
            }
            media.Delete(site.LogoPath);
            site.LogoPath = saved.WebPath;
        }
        else if (removeLogo)
        {
            media.Delete(site.LogoPath);
            site.LogoPath = null;
        }

        // A value equal to the built-in default is stored as null, so a later change of the default still reaches this site.
        site.ClinicNameFa = Custom(Input.ClinicNameFa, l.WithCulture("fa", "Dr. Mahsa Hassani Acupuncture Clinic"));
        site.ClinicNameEn = Custom(Input.ClinicNameEn, "Dr. Mahsa Hassani Acupuncture Clinic");
        site.BrandTitleFa = Custom(Input.BrandTitleFa, l.WithCulture("fa", "Acupuncture Clinic"));
        site.BrandTitleEn = Custom(Input.BrandTitleEn, "Acupuncture Clinic");
        site.BrandSubtitleFa = Custom(Input.BrandSubtitleFa, l.WithCulture("fa", "Dr. Mahsa Hassani"));
        site.BrandSubtitleEn = Custom(Input.BrandSubtitleEn, "Dr. Mahsa Hassani");
        site.HeroEyebrowFa = Custom(Input.HeroEyebrowFa, l.WithCulture("fa", "Acupuncture and integrative medicine clinic"));
        site.HeroEyebrowEn = Custom(Input.HeroEyebrowEn, "Acupuncture and integrative medicine clinic");
        site.HeroTitleFa = Input.HeroTitleFa.Trim();
        site.HeroTitleEn = Input.HeroTitleEn.Trim();
        site.ThemeColor = ThemePalette.Normalize(Input.ThemeColor, ThemePalette.DefaultTheme);
        site.AccentColor = ThemePalette.Normalize(Input.AccentColor, ThemePalette.DefaultAccent);
        site.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        TempData["Message"] = l["The clinic's name, logo and colours were saved."].Value;
        return RedirectToPage();
    }

    /// <summary>Back to the built-in olive colours; names and logo stay.</summary>
    public async Task<IActionResult> OnPostResetColorsAsync()
    {
        var site = await LoadAsync();
        site.ThemeColor = null;
        site.AccentColor = null;
        site.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        TempData["Message"] = l["The original olive colours are back."].Value;
        return RedirectToPage();
    }

    private async Task<SiteContent> LoadAsync()
    {
        var site = await db.SiteContent.FindAsync(SiteContent.SingletonId);
        if (site is null)
        {
            site = new SiteContent();
            db.SiteContent.Add(site);
        }
        LogoPath = site.LogoPath ?? Brand.DefaultLogo;
        CustomLogo = site.LogoPath is not null;
        return site;
    }

    private static string? Custom(string? value, string builtIn)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) || value == builtIn ? null : value;
    }
}
