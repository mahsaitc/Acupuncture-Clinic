namespace Clinic.Domain.Entities;

/// <summary>Editable public content of the site: the home page hero and the clinic's contact details. A single row.</summary>
public class SiteContent
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string HeroTitleFa { get; set; } = "طب سوزنی، راهی طبیعی به سلامتی";
    public string HeroTitleEn { get; set; } = "Acupuncture, a natural path to wellbeing";
    public string HeroSubtitleFa { get; set; } = "طب سوزنی، کاشت نخ برای مدیریت وزن و پزشکی خانواده با بیش از ۲۰ سال تجربه بالینی.";
    public string HeroSubtitleEn { get; set; } = "Acupuncture, catgut embedding for weight management and family medicine, backed by over 20 years of clinical experience.";

    /// <summary>Web path of the uploaded hero video, e.g. /media/hero/abc.mp4. Null shows the still background.</summary>
    public string? HeroVideoPath { get; set; }

    /// <summary>Image shown before the video loads, on slow connections and for visitors who prefer reduced motion.</summary>
    public string? HeroPosterPath { get; set; }

    public string? Phone { get; set; } = "07132346425";
    public string? AddressFa { get; set; }
    public string? AddressEn { get; set; }
    public string? Email { get; set; }
    public string? OpeningHoursFa { get; set; }
    public string? OpeningHoursEn { get; set; }

    /// <summary>The src of Google Maps' "Embed a map" iframe (https://www.google.com/maps/embed?pb=...).</summary>
    public string? MapEmbedUrl { get; set; }

    /// <summary>A normal Google Maps link to the clinic, for "Get directions".</summary>
    public string? MapLinkUrl { get; set; }

    public string? InstagramUrl { get; set; }
    public string? WhatsAppUrl { get; set; }
    public string? TelegramUrl { get; set; }
    public string? YouTubeUrl { get; set; }

    /// <summary>The home page's treatment results (<see cref="ClinicResults"/>) as JSON; null shows the sample figures.</summary>
    public string? ResultsJson { get; set; }

    // Branding, edited only by the site owner. Null keeps the built-in name, logo and olive colours.

    /// <summary>The clinic's full name: browser tab, footer, letterheads of the printouts.</summary>
    public string? ClinicNameFa { get; set; }
    public string? ClinicNameEn { get; set; }

    /// <summary>The two lines beside the logo in the menu, e.g. "Acupuncture Clinic" over "Dr. ...".</summary>
    public string? BrandTitleFa { get; set; }
    public string? BrandTitleEn { get; set; }
    public string? BrandSubtitleFa { get; set; }
    public string? BrandSubtitleEn { get; set; }

    /// <summary>The small label above the home page title.</summary>
    public string? HeroEyebrowFa { get; set; }
    public string? HeroEyebrowEn { get; set; }

    /// <summary>Web path of the uploaded logo, e.g. /media/brand/abc.png.</summary>
    public string? LogoPath { get; set; }

    /// <summary>Main theme colour as #rrggbb (replaces olive) and the accent colour (replaces sand).</summary>
    public string? ThemeColor { get; set; }
    public string? AccentColor { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public string HeroTitle(bool english) => english ? HeroTitleEn : HeroTitleFa;
    public string HeroSubtitle(bool english) => english ? HeroSubtitleEn : HeroSubtitleFa;
    public string? Address(bool english) => english ? AddressEn : AddressFa;
    public string? OpeningHours(bool english) => english ? OpeningHoursEn : OpeningHoursFa;

    public ClinicResults Results()
    {
        if (string.IsNullOrWhiteSpace(ResultsJson))
        {
            return ClinicResults.Sample();
        }
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<ClinicResults>(ResultsJson) ?? ClinicResults.Sample();
        }
        catch (System.Text.Json.JsonException)
        {
            return ClinicResults.Sample();
        }
    }

    public bool HasSocialLinks => InstagramUrl is not null || WhatsAppUrl is not null || TelegramUrl is not null || YouTubeUrl is not null;
}
