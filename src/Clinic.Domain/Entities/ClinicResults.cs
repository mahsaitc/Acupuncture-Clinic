namespace Clinic.Domain.Entities;

/// <summary>
/// The treatment results shown at the bottom of the home page: headline figures and charts the admin enters. Kept as
/// JSON in <see cref="SiteContent.ResultsJson"/>. The first figures are examples, flagged by <see cref="IsSample"/>, and
/// the page says so until the admin replaces them with the clinic's own data.
/// </summary>
public class ClinicResults
{
    public bool Show { get; set; } = true;

    /// <summary>True while the figures are examples; the home page then marks them as sample data.</summary>
    public bool IsSample { get; set; } = true;

    public string TitleFa { get; set; } = "";
    public string TitleEn { get; set; } = "";

    /// <summary>Where the figures come from: number of patients, period, "results vary from person to person".</summary>
    public string? NoteFa { get; set; }
    public string? NoteEn { get; set; }

    public List<ResultTile> Tiles { get; set; } = [];
    public List<ResultChart> Charts { get; set; } = [];

    public string Title(bool english) => english ? TitleEn : TitleFa;
    public string? Note(bool english) => english ? NoteEn : NoteFa;

    /// <summary>Example figures to show the layout. They are not clinic data.</summary>
    public static ClinicResults Sample() => new()
    {
        TitleFa = "نتایج درمان در کلینیک",
        TitleEn = "Treatment results at the clinic",
        NoteFa = "این ارقام نمونه هستند و باید با داده‌های واقعی کلینیک جایگزین شوند. نتیجه درمان در هر فرد متفاوت است.",
        NoteEn = "These are sample figures, to be replaced with the clinic's own data. Results vary from person to person.",
        Tiles =
        [
            new() { Icon = "people", Value = "1200+", LabelFa = "بیمار درمان‌شده", LabelEn = "patients treated" },
            new() { Icon = "speedometer2", Value = "8.4", LabelFa = "کیلوگرم میانگین کاهش وزن در دوره ۱۲ جلسه‌ای", LabelEn = "kg average weight loss over a 12-session course" },
            new() { Icon = "activity", Value = "60%", LabelFa = "میانگین کاهش شدت درد", LabelEn = "average reduction in pain score" },
            new() { Icon = "emoji-smile", Value = "91%", LabelFa = "رضایت بیماران", LabelEn = "patient satisfaction" },
        ],
        Charts =
        [
            new()
            {
                TitleFa = "میانگین کاهش وزن با کاشت نخ و طب سوزنی", TitleEn = "Average weight loss with catgut embedding and acupuncture",
                Kind = ResultChartKind.Bars, UnitFa = "کیلوگرم", UnitEn = "kg",
                Rows =
                [
                    new() { LabelFa = "۴ جلسه", LabelEn = "4 sessions", Value = 3.1 },
                    new() { LabelFa = "۸ جلسه", LabelEn = "8 sessions", Value = 6.2 },
                    new() { LabelFa = "۱۲ جلسه", LabelEn = "12 sessions", Value = 8.4 },
                    new() { LabelFa = "۱۶ جلسه", LabelEn = "16 sessions", Value = 10.9 },
                ],
            },
            new()
            {
                TitleFa = "شدت درد قبل و بعد از دوره درمان (۰ تا ۱۰)", TitleEn = "Pain score before and after a course of treatment (0 to 10)",
                Kind = ResultChartKind.BeforeAfter, UnitFa = "", UnitEn = "", ShowTable = true,
                Rows =
                [
                    new() { LabelFa = "کمردرد", LabelEn = "Low back pain", Value = 7.8, After = 3.1 },
                    new() { LabelFa = "زانودرد", LabelEn = "Knee pain", Value = 7.2, After = 3.4 },
                    new() { LabelFa = "میگرن", LabelEn = "Migraine", Value = 8.1, After = 3.9 },
                    new() { LabelFa = "گردن و شانه", LabelEn = "Neck and shoulder", Value = 6.9, After = 2.8 },
                    new() { LabelFa = "سیاتیک", LabelEn = "Sciatica", Value = 7.6, After = 3.6 },
                ],
            },
            new()
            {
                TitleFa = "بیمارانی که به هدف درمان رسیدند", TitleEn = "Patients who reached their treatment goal",
                Kind = ResultChartKind.Bars, UnitFa = "٪", UnitEn = "%",
                Rows =
                [
                    new() { LabelFa = "کاهش وزن", LabelEn = "Weight loss", Value = 78 },
                    new() { LabelFa = "کاهش درد", LabelEn = "Pain relief", Value = 84 },
                    new() { LabelFa = "بهبود خواب", LabelEn = "Better sleep", Value = 71 },
                    new() { LabelFa = "ترک سیگار", LabelEn = "Quitting smoking", Value = 58 },
                ],
            },
        ],
    };
}

/// <summary>A headline figure, like "91%" with "patient satisfaction".</summary>
public class ResultTile
{
    /// <summary>A Bootstrap Icons name without the "bi-" prefix.</summary>
    public string Icon { get; set; } = "graph-up";
    public string Value { get; set; } = "";
    public string LabelFa { get; set; } = "";
    public string LabelEn { get; set; } = "";

    public string Label(bool english) => english ? LabelEn : LabelFa;
}

public enum ResultChartKind
{
    /// <summary>One bar per row.</summary>
    Bars = 0,

    /// <summary>Two bars per row: before treatment (Value) and after (After).</summary>
    BeforeAfter = 1,
}

public class ResultChart
{
    public string TitleFa { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public ResultChartKind Kind { get; set; }
    public string? UnitFa { get; set; }
    public string? UnitEn { get; set; }

    /// <summary>Also lists the figures in a table under the chart.</summary>
    public bool ShowTable { get; set; }

    public List<ResultRow> Rows { get; set; } = [];

    public string Title(bool english) => english ? TitleEn : TitleFa;
    public string? Unit(bool english) => english ? UnitEn : UnitFa;
}

public class ResultRow
{
    public string LabelFa { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public double Value { get; set; }

    /// <summary>The after-treatment value, for before-and-after charts.</summary>
    public double? After { get; set; }

    public string Label(bool english) => english ? LabelEn : LabelFa;
}
