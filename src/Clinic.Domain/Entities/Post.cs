namespace Clinic.Domain.Entities;

public enum PostKind
{
    /// <summary>The doctor's blog: news from the clinic, tips, patient education.</summary>
    Blog = 0,

    /// <summary>Medical and acupuncture articles, usually with references.</summary>
    Article = 1,
}

/// <summary>A blog post or medical article. Persian is required; English is optional and shown only when filled.</summary>
public class Post
{
    public int Id { get; set; }
    public PostKind Kind { get; set; }

    /// <summary>URL part, e.g. "acupuncture-for-back-pain". Unique per kind.</summary>
    public string Slug { get; set; } = default!;

    public string TitleFa { get; set; } = default!;
    public string? SummaryFa { get; set; }

    /// <summary>Markdown.</summary>
    public string BodyFa { get; set; } = default!;

    public string? TitleEn { get; set; }
    public string? SummaryEn { get; set; }
    public string? BodyEn { get; set; }

    public string? CoverImagePath { get; set; }

    /// <summary>Optional references, one per line (e.g. PubMed links).</summary>
    public string? References { get; set; }

    public string AuthorUserId { get; set; } = default!;
    public bool IsPublished { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool HasEnglish => !string.IsNullOrWhiteSpace(TitleEn) && !string.IsNullOrWhiteSpace(BodyEn);

    public string Title(bool english) => english && HasEnglish ? TitleEn! : TitleFa;
    public string? Summary(bool english) => english && HasEnglish ? SummaryEn : SummaryFa;
    public string Body(bool english) => english && HasEnglish ? BodyEn! : BodyFa;
}
