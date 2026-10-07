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

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public string HeroTitle(bool english) => english ? HeroTitleEn : HeroTitleFa;
    public string HeroSubtitle(bool english) => english ? HeroSubtitleEn : HeroSubtitleFa;
    public string? Address(bool english) => english ? AddressEn : AddressFa;
    public string? OpeningHours(bool english) => english ? OpeningHoursEn : OpeningHoursFa;

    public bool HasSocialLinks => InstagramUrl is not null || WhatsAppUrl is not null || TelegramUrl is not null || YouTubeUrl is not null;
}
