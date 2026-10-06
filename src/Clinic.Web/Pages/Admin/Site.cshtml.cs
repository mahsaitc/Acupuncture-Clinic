using System.ComponentModel.DataAnnotations;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

/// <summary>Home page hero (text, video, poster) and the clinic's contact details.</summary>
[RequestSizeLimit(MediaStore.MaxVideoBytes + MediaStore.MaxImageBytes + 1024 * 1024)]
public class SiteModel(ClinicDbContext db, MediaStore media, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty]
    public ContentInput Input { get; set; } = new();

    public string? VideoPath { get; private set; }
    public string? PosterPath { get; private set; }

    public class ContentInput
    {
        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Hero title (Persian)")]
        public string HeroTitleFa { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(200)]
        [Display(Name = "Hero title (English)")]
        public string HeroTitleEn { get; set; } = "";

        [StringLength(500)]
        [Display(Name = "Hero subtitle (Persian)")]
        public string? HeroSubtitleFa { get; set; }

        [StringLength(500)]
        [Display(Name = "Hero subtitle (English)")]
        public string? HeroSubtitleEn { get; set; }

        [StringLength(30)]
        [Display(Name = "Clinic phone")]
        public string? Phone { get; set; }

        [StringLength(300)]
        [Display(Name = "Address (Persian)")]
        public string? AddressFa { get; set; }

        [StringLength(300)]
        [Display(Name = "Address (English)")]
        public string? AddressEn { get; set; }

        [Url(ErrorMessage = "Enter a full link starting with https://")]
        [Display(Name = "Instagram link")]
        public string? InstagramUrl { get; set; }

        [Url(ErrorMessage = "Enter a full link starting with https://")]
        [Display(Name = "WhatsApp link")]
        public string? WhatsAppUrl { get; set; }

        [Url(ErrorMessage = "Enter a full link starting with https://")]
        [Display(Name = "Telegram link")]
        public string? TelegramUrl { get; set; }

        [Url(ErrorMessage = "Enter a full link starting with https://")]
        [Display(Name = "YouTube link")]
        public string? YouTubeUrl { get; set; }
    }

    public async Task OnGetAsync()
    {
        var content = await LoadAsync();
        Input = new ContentInput
        {
            HeroTitleFa = content.HeroTitleFa,
            HeroTitleEn = content.HeroTitleEn,
            HeroSubtitleFa = content.HeroSubtitleFa,
            HeroSubtitleEn = content.HeroSubtitleEn,
            Phone = content.Phone,
            AddressFa = content.AddressFa,
            AddressEn = content.AddressEn,
            InstagramUrl = content.InstagramUrl,
            WhatsAppUrl = content.WhatsAppUrl,
            TelegramUrl = content.TelegramUrl,
            YouTubeUrl = content.YouTubeUrl,
        };
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? video, IFormFile? poster, bool removeVideo)
    {
        var content = await LoadAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (video is { Length: > 0 })
        {
            var saved = await media.SaveVideoAsync(video);
            if (!AddUploadError(saved, l["The video must be an MP4 or WebM file of at most 150 MB."]))
            {
                media.Delete(content.HeroVideoPath);
                content.HeroVideoPath = saved.WebPath;
            }
        }
        else if (removeVideo)
        {
            media.Delete(content.HeroVideoPath);
            content.HeroVideoPath = null;
        }

        if (poster is { Length: > 0 })
        {
            var saved = await media.SaveImageAsync(poster);
            if (!AddUploadError(saved, l["The cover image must be a JPG, PNG or WebP file of at most 5 MB."]))
            {
                media.Delete(content.HeroPosterPath);
                content.HeroPosterPath = saved.WebPath;
            }
        }

        if (!ModelState.IsValid)
        {
            VideoPath = content.HeroVideoPath;
            PosterPath = content.HeroPosterPath;
            await db.SaveChangesAsync();
            return Page();
        }

        content.HeroTitleFa = Input.HeroTitleFa.Trim();
        content.HeroTitleEn = Input.HeroTitleEn.Trim();
        content.HeroSubtitleFa = Input.HeroSubtitleFa?.Trim() ?? "";
        content.HeroSubtitleEn = Input.HeroSubtitleEn?.Trim() ?? "";
        content.Phone = Clean(Input.Phone);
        content.AddressFa = Clean(Input.AddressFa);
        content.AddressEn = Clean(Input.AddressEn);
        content.InstagramUrl = Clean(Input.InstagramUrl);
        content.WhatsAppUrl = Clean(Input.WhatsAppUrl);
        content.TelegramUrl = Clean(Input.TelegramUrl);
        content.YouTubeUrl = Clean(Input.YouTubeUrl);
        content.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        TempData["Message"] = l["The site content was saved."].Value;
        return RedirectToPage();
    }

    private bool AddUploadError(MediaStore.SaveResult saved, string message)
    {
        if (saved.Error == MediaStore.SaveError.None)
        {
            return false;
        }
        ModelState.AddModelError(string.Empty, message);
        return true;
    }

    private async Task<SiteContent> LoadAsync()
    {
        var content = await db.SiteContent.FindAsync(SiteContent.SingletonId);
        if (content is null)
        {
            content = new SiteContent();
            db.SiteContent.Add(content);
        }
        VideoPath = content.HeroVideoPath;
        PosterPath = content.HeroPosterPath;
        return content;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
