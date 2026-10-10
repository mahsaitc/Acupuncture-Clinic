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

    /// <summary>What patients say, shown as cards on the home page.</summary>
    public List<Testimonial> Testimonials { get; set; } = [];

    /// <summary>"Why our clinic" items: a picture or an icon with a short text.</summary>
    public List<Highlight> Highlights { get; set; } = [];

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
            new()
            {
                TitleFa = "میانگین رضایت بیماران بر اساس نوع بیماری", TitleEn = "Average patient satisfaction by condition",
                Kind = ResultChartKind.Bars, UnitFa = "٪", UnitEn = "%",
                Rows =
                [
                    new() { LabelFa = "اضافه وزن و چاقی", LabelEn = "Overweight and obesity", Value = 92 },
                    new() { LabelFa = "کمردرد", LabelEn = "Low back pain", Value = 90 },
                    new() { LabelFa = "زانودرد", LabelEn = "Knee pain", Value = 88 },
                    new() { LabelFa = "میگرن و سردرد", LabelEn = "Migraine and headache", Value = 86 },
                    new() { LabelFa = "بی‌خوابی و استرس", LabelEn = "Insomnia and stress", Value = 84 },
                ],
            },
            new()
            {
                TitleFa = "میانگین رضایت بیماران بر اساس نوع درمان", TitleEn = "Average patient satisfaction by treatment",
                Kind = ResultChartKind.Bars, UnitFa = "٪", UnitEn = "%",
                Rows =
                [
                    new() { LabelFa = "طب سوزنی", LabelEn = "Acupuncture", Value = 91 },
                    new() { LabelFa = "کاشت نخ (کت‌گوت)", LabelEn = "Catgut embedding", Value = 89 },
                    new() { LabelFa = "الکترواکوپانکچر", LabelEn = "Electroacupuncture", Value = 88 },
                    new() { LabelFa = "بادکش درمانی", LabelEn = "Cupping", Value = 87 },
                    new() { LabelFa = "گوش درمانی", LabelEn = "Auricular therapy", Value = 85 },
                ],
            },
        ],
        Testimonials =
        [
            new()
            {
                NameFa = "خانم م. ر.", NameEn = "Mrs. M. R.", ConditionFa = "اضافه وزن", ConditionEn = "Overweight", TreatmentFa = "کاشت نخ", TreatmentEn = "Catgut embedding", Rating = 5,
                TextFa = "بعد از چند جلسه کاشت نخ، کنترل اشتها برایم خیلی راحت‌تر شد و با برنامه غذایی‌ای که گرفتم وزنم کم شد. برخورد پزشک و پیگیری‌ها عالی بود.",
                TextEn = "After a few embedding sessions my appetite was much easier to control, and with the eating plan I was given I lost weight. The doctor's care and follow-up were excellent.",
            },
            new()
            {
                NameFa = "آقای ع. ک.", NameEn = "Mr. A. K.", ConditionFa = "کمردرد مزمن", ConditionEn = "Chronic low back pain", TreatmentFa = "طب سوزنی", TreatmentEn = "Acupuncture", Rating = 5,
                TextFa = "سال‌ها کمردرد داشتم. با جلسات طب سوزنی دردم به‌مراتب کمتر شد و حالا راحت‌تر کار می‌کنم و می‌خوابم.",
                TextEn = "I had back pain for years. With acupuncture sessions it became much milder, and now I work and sleep more comfortably.",
            },
            new()
            {
                NameFa = "خانم س. ن.", NameEn = "Mrs. S. N.", ConditionFa = "میگرن", ConditionEn = "Migraine", TreatmentFa = "طب سوزنی و گوش درمانی", TreatmentEn = "Acupuncture and auricular therapy", Rating = 4,
                TextFa = "دفعات سردردهایم کمتر شده و شدتشان هم پایین آمده. محیط کلینیک آرام و تمیز است.",
                TextEn = "My headaches come less often and are less severe. The clinic is calm and clean.",
            },
        ],
        Highlights =
        [
            new() { Icon = "award", TitleFa = "بیش از ۲۰ سال تجربه بالینی", TitleEn = "Over 20 years of clinical experience", TextFa = "درمان زیر نظر پزشک عمومی با سابقه طولانی در طب سوزنی و کاشت نخ.", TextEn = "Treatment by a GP with long experience in acupuncture and catgut embedding." },
            new() { Icon = "shield-check", TitleFa = "سوزن و نخ استریل یک‌بارمصرف", TitleEn = "Sterile single-use needles and threads", TextFa = "رعایت کامل اصول بهداشت و استانداردهای ایمنی در هر جلسه.", TextEn = "Full hygiene and safety standards at every session." },
            new() { Icon = "clipboard2-pulse", TitleFa = "برنامه درمانی شخصی", TitleEn = "A personal treatment plan", TextFa = "ارزیابی کامل و طراحی برنامه متناسب با شرایط و هدف هر بیمار.", TextEn = "A full assessment and a plan built around each patient's needs and goals." },
            new() { Icon = "graph-down-arrow", TitleFa = "پیگیری پیشرفت درمان", TitleEn = "Progress you can follow", TextFa = "ثبت وزن، شدت درد و روند بهبود در پرونده الکترونیک شما.", TextEn = "Weight, pain score and progress recorded in your electronic file." },
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

/// <summary>A patient's words about their treatment. Shown only with their consent; no full names.</summary>
public class Testimonial
{
    public string NameFa { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? ConditionFa { get; set; }
    public string? ConditionEn { get; set; }
    public string? TreatmentFa { get; set; }
    public string? TreatmentEn { get; set; }

    /// <summary>1 to 5 stars.</summary>
    public int Rating { get; set; } = 5;
    public string TextFa { get; set; } = "";
    public string TextEn { get; set; } = "";

    /// <summary>Web path of an uploaded picture, or null for an initial.</summary>
    public string? PhotoPath { get; set; }

    public string Name(bool english) => english ? NameEn : NameFa;
    public string? Condition(bool english) => english ? ConditionEn : ConditionFa;
    public string? Treatment(bool english) => english ? TreatmentEn : TreatmentFa;
    public string Text(bool english) => english ? TextEn : TextFa;
}

/// <summary>A promotional "why our clinic" item.</summary>
public class Highlight
{
    public string Icon { get; set; } = "star";
    public string TitleFa { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string? TextFa { get; set; }
    public string? TextEn { get; set; }

    /// <summary>Web path of an uploaded picture; without one the icon is shown on a coloured tile.</summary>
    public string? ImagePath { get; set; }

    public string Title(bool english) => english ? TitleEn : TitleFa;
    public string? Text(bool english) => english ? TextEn : TextFa;
}
