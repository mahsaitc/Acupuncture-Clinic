using System.Text.Json;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

/// <summary>The admin edits the treatment results shown at the bottom of the home page.</summary>
[Authorize(Policy = Policies.Admin)]
[RequestSizeLimit(40L * 1024 * 1024)]
public class ResultsModel(ClinicDbContext db, Clinic.Web.Media.MediaStore media, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    /// <summary>Empty rows offered for new entries.</summary>
    public const int BlankTiles = 2, BlankRows = 3;

    public static readonly string[] Icons = ["people", "speedometer2", "activity", "emoji-smile", "heart-pulse", "graph-down-arrow", "graph-up-arrow", "award", "calendar-check", "moon-stars", "star", "shield-check", "clipboard2-pulse", "hand-thumbs-up", "flower1", "droplet"];

    [BindProperty]
    public ClinicResults Results { get; set; } = new();

    public async Task OnGetAsync()
    {
        Results = (await db.SiteContent.AsNoTracking().FirstOrDefaultAsync(c => c.Id == SiteContent.SingletonId) ?? new SiteContent()).Results();
        AddBlanks();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Pictures are matched to their rows before empty rows are dropped.
        for (var i = 0; i < Results.Testimonials.Count; i++)
        {
            Results.Testimonials[i].PhotoPath = await PictureAsync(Results.Testimonials[i].PhotoPath, $"tphoto{i}", $"tremove{i}");
        }
        for (var i = 0; i < Results.Highlights.Count; i++)
        {
            Results.Highlights[i].ImagePath = await PictureAsync(Results.Highlights[i].ImagePath, $"hphoto{i}", $"hremove{i}");
        }
        var results = Tidy(Results);
        var content = await LoadAsync();
        content.ResultsJson = JsonSerializer.Serialize(results);
        content.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        TempData["Message"] = l["The results on the home page were saved."].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSampleAsync()
    {
        var content = await LoadAsync();
        content.ResultsJson = null;
        content.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        TempData["Message"] = l["The sample figures were restored."].Value;
        return RedirectToPage();
    }

    /// <summary>Keeps, replaces or removes a row's picture from the form's file and "remove" fields.</summary>
    private async Task<string?> PictureAsync(string? current, string fileField, string removeField)
    {
        // Only pictures this store saved for the home page may be kept.
        var path = current is not null && current.StartsWith(Clinic.Web.Media.MediaStore.RequestPath + "/home/", StringComparison.Ordinal) ? current : null;
        if (Request.HasFormContentType && Request.Form.Files.GetFile(fileField) is { Length: > 0 } file)
        {
            var saved = await media.SaveHomeImageAsync(file);
            if (saved.WebPath is not null)
            {
                return saved.WebPath;
            }
            TempData["Error"] = l["{0} could not be used: choose a JPG, PNG or WEBP image of at most 5 MB.", file.FileName].Value;
        }
        if (Request.HasFormContentType && Request.Form[removeField] == "true")
        {
            return null;
        }
        return path;
    }

    /// <summary>Drops the rows and charts left empty, so clearing a row removes it.</summary>
    public static ClinicResults Tidy(ClinicResults input)
    {
        static string T(string? s) => s?.Trim() ?? "";
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        return new ClinicResults
        {
            Show = input.Show,
            IsSample = input.IsSample,
            TitleFa = T(input.TitleFa),
            TitleEn = T(input.TitleEn),
            NoteFa = N(input.NoteFa),
            NoteEn = N(input.NoteEn),
            Tiles = input.Tiles
                .Where(t => !string.IsNullOrWhiteSpace(t.Value) && !(string.IsNullOrWhiteSpace(t.LabelFa) && string.IsNullOrWhiteSpace(t.LabelEn)))
                .Select(t => new ResultTile
                {
                    Icon = Icons.Contains(t.Icon) ? t.Icon : Icons[0],
                    Value = Clinic.Application.Common.JalaliDate.ToLatinDigits(t.Value.Trim()).Replace('٪', '%'),
                    LabelFa = T(t.LabelFa),
                    LabelEn = T(t.LabelEn),
                })
                .ToList(),
            Charts = input.Charts
                .Select(c => new ResultChart
                {
                    TitleFa = T(c.TitleFa),
                    TitleEn = T(c.TitleEn),
                    Kind = Enum.IsDefined(c.Kind) ? c.Kind : ResultChartKind.Bars,
                    UnitFa = N(c.UnitFa),
                    UnitEn = N(c.UnitEn),
                    ShowTable = c.ShowTable,
                    Rows = c.Rows
                        .Where(r => !(string.IsNullOrWhiteSpace(r.LabelFa) && string.IsNullOrWhiteSpace(r.LabelEn)))
                        .Select(r => new ResultRow { LabelFa = T(r.LabelFa), LabelEn = T(r.LabelEn), Value = r.Value, After = c.Kind == ResultChartKind.BeforeAfter ? r.After : null })
                        .ToList(),
                })
                .Where(c => c.Rows.Count > 0 || c.TitleFa.Length > 0 || c.TitleEn.Length > 0)
                .ToList(),
            Testimonials = input.Testimonials
                .Where(t => !(string.IsNullOrWhiteSpace(t.TextFa) && string.IsNullOrWhiteSpace(t.TextEn)))
                .Select(t => new Testimonial
                {
                    NameFa = T(t.NameFa), NameEn = T(t.NameEn), ConditionFa = N(t.ConditionFa), ConditionEn = N(t.ConditionEn),
                    TreatmentFa = N(t.TreatmentFa), TreatmentEn = N(t.TreatmentEn), Rating = Math.Clamp(t.Rating, 1, 5),
                    TextFa = T(t.TextFa), TextEn = T(t.TextEn), PhotoPath = t.PhotoPath,
                })
                .ToList(),
            Highlights = input.Highlights
                .Where(h => !(string.IsNullOrWhiteSpace(h.TitleFa) && string.IsNullOrWhiteSpace(h.TitleEn)))
                .Select(h => new Highlight
                {
                    Icon = Icons.Contains(h.Icon) ? h.Icon : "star", TitleFa = T(h.TitleFa), TitleEn = T(h.TitleEn),
                    TextFa = N(h.TextFa), TextEn = N(h.TextEn), ImagePath = h.ImagePath,
                })
                .ToList(),
        };
    }

    private void AddBlanks()
    {
        for (var i = 0; i < BlankTiles; i++)
        {
            Results.Tiles.Add(new ResultTile());
        }
        Results.Charts.Add(new ResultChart());
        Results.Testimonials.Add(new Testimonial());
        Results.Highlights.Add(new Highlight());
        foreach (var c in Results.Charts)
        {
            for (var i = 0; i < (c.Rows.Count == 0 ? BlankRows + 2 : BlankRows); i++)
            {
                c.Rows.Add(new ResultRow());
            }
        }
    }

    private async Task<SiteContent> LoadAsync()
    {
        var content = await db.SiteContent.FindAsync(SiteContent.SingletonId);
        if (content is null)
        {
            content = new SiteContent();
            db.SiteContent.Add(content);
        }
        return content;
    }
}
