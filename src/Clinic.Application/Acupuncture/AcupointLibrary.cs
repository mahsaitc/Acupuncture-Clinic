using Clinic.Domain.Entities;

namespace Clinic.Application.Acupuncture;

public enum BodyRegion
{
    Head = 0,
    Trunk = 1,
    Back = 2,
    Arm = 3,
    Leg = 4,
}

/// <summary>
/// A standard point placed on the body diagram. Dx is the distance from the midline toward the
/// patient's side; Dx = 0 is a midline point, anything else exists on both sides.
/// </summary>
public sealed record Acupoint(string Code, string Pinyin, BodyRegion Region, BodyView View, double Dx, double Y)
{
    public bool Bilateral => Dx > 0;

    public string Label => $"{Code} {Pinyin}";
}

public sealed record Protocol(string Key, string Name, IReadOnlyList<string> Codes);

/// <summary>
/// Commonly used points and their approximate place on the clinic's body diagram.
/// The diagram is a guide for recording, not an anatomical reference.
/// </summary>
public static class AcupointLibrary
{
    public const double Width = 200;
    public const double Height = 480;
    public const double Midline = Width / 2;

    public static readonly IReadOnlyList<Acupoint> Points =
    [
        // Head and face, front.
        new("GV20", "Baihui", BodyRegion.Head, BodyView.Front, 0, 16),
        new("GB14", "Yangbai", BodyRegion.Head, BodyView.Front, 8, 27),
        new("EX-HN3", "Yintang", BodyRegion.Head, BodyView.Front, 0, 33),
        new("EX-HN5", "Taiyang", BodyRegion.Head, BodyView.Front, 17, 35),
        new("LI20", "Yingxiang", BodyRegion.Head, BodyView.Front, 6, 46),
        new("GV26", "Shuigou", BodyRegion.Head, BodyView.Front, 0, 51),
        new("ST6", "Jiache", BodyRegion.Head, BodyView.Front, 14, 56),

        // Chest and abdomen.
        new("CV22", "Tiantu", BodyRegion.Trunk, BodyView.Front, 0, 90),
        new("CV17", "Danzhong", BodyRegion.Trunk, BodyView.Front, 0, 125),
        new("LR14", "Qimen", BodyRegion.Trunk, BodyView.Front, 25, 148),
        new("CV12", "Zhongwan", BodyRegion.Trunk, BodyView.Front, 0, 172),
        new("ST21", "Liangmen", BodyRegion.Trunk, BodyView.Front, 12, 172),
        new("LR13", "Zhangmen", BodyRegion.Trunk, BodyView.Front, 30, 182),
        new("CV8", "Shenque", BodyRegion.Trunk, BodyView.Front, 0, 200),
        new("ST25", "Tianshu", BodyRegion.Trunk, BodyView.Front, 13, 200),
        new("SP15", "Daheng", BodyRegion.Trunk, BodyView.Front, 25, 200),
        new("CV6", "Qihai", BodyRegion.Trunk, BodyView.Front, 0, 212),
        new("CV4", "Guanyuan", BodyRegion.Trunk, BodyView.Front, 0, 226),
        new("ST28", "Shuidao", BodyRegion.Trunk, BodyView.Front, 13, 228),
        new("CV3", "Zhongji", BodyRegion.Trunk, BodyView.Front, 0, 237),

        // Arm, palm side (anatomical position).
        new("LU5", "Chize", BodyRegion.Arm, BodyView.Front, 61, 184),
        new("LI11", "Quchi", BodyRegion.Arm, BodyView.Front, 66, 188),
        new("LI10", "Shousanli", BodyRegion.Arm, BodyView.Front, 66, 202),
        new("PC6", "Neiguan", BodyRegion.Arm, BodyView.Front, 63, 240),
        new("LU7", "Lieque", BodyRegion.Arm, BodyView.Front, 69, 246),
        new("HT7", "Shenmen", BodyRegion.Arm, BodyView.Front, 60, 257),
        new("LI4", "Hegu", BodyRegion.Arm, BodyView.Front, 73, 276),

        // Leg, front.
        new("GB31", "Fengshi", BodyRegion.Leg, BodyView.Front, 33, 290),
        new("ST34", "Liangqiu", BodyRegion.Leg, BodyView.Front, 30, 324),
        new("SP10", "Xuehai", BodyRegion.Leg, BodyView.Front, 9, 324),
        new("SP9", "Yinlingquan", BodyRegion.Leg, BodyView.Front, 11, 362),
        new("GB34", "Yanglingquan", BodyRegion.Leg, BodyView.Front, 29, 362),
        new("ST36", "Zusanli", BodyRegion.Leg, BodyView.Front, 26, 370),
        new("ST40", "Fenglong", BodyRegion.Leg, BodyView.Front, 27, 396),
        new("SP6", "Sanyinjiao", BodyRegion.Leg, BodyView.Front, 12, 410),
        new("GB39", "Xuanzhong", BodyRegion.Leg, BodyView.Front, 28, 416),
        new("KI3", "Taixi", BodyRegion.Leg, BodyView.Front, 11, 438),
        new("LR3", "Taichong", BodyRegion.Leg, BodyView.Front, 18, 452),
        new("ST44", "Neiting", BodyRegion.Leg, BodyView.Front, 22, 461),

        // Head, neck and back, seen from behind.
        new("GB20", "Fengchi", BodyRegion.Back, BodyView.Back, 9, 58),
        new("GV14", "Dazhui", BodyRegion.Back, BodyView.Back, 0, 88),
        new("GB21", "Jianjing", BodyRegion.Back, BodyView.Back, 24, 92),
        new("BL13", "Feishu", BodyRegion.Back, BodyView.Back, 11, 112),
        new("SI11", "Tianzong", BodyRegion.Back, BodyView.Back, 26, 120),
        new("BL15", "Xinshu", BodyRegion.Back, BodyView.Back, 11, 126),
        new("BL17", "Geshu", BodyRegion.Back, BodyView.Back, 11, 142),
        new("BL18", "Ganshu", BodyRegion.Back, BodyView.Back, 11, 160),
        new("BL20", "Pishu", BodyRegion.Back, BodyView.Back, 11, 176),
        new("BL21", "Weishu", BodyRegion.Back, BodyView.Back, 11, 187),
        new("GV4", "Mingmen", BodyRegion.Back, BodyView.Back, 0, 205),
        new("BL23", "Shenshu", BodyRegion.Back, BodyView.Back, 11, 205),
        new("BL52", "Zhishi", BodyRegion.Back, BodyView.Back, 22, 205),
        new("BL25", "Dachangshu", BodyRegion.Back, BodyView.Back, 11, 222),
        new("GB30", "Huantiao", BodyRegion.Back, BodyView.Back, 28, 252),

        // Arm and leg, seen from behind.
        new("SJ5", "Waiguan", BodyRegion.Arm, BodyView.Back, 63, 240),
        new("BL40", "Weizhong", BodyRegion.Leg, BodyView.Back, 20, 345),
        new("BL57", "Chengshan", BodyRegion.Leg, BodyView.Back, 20, 388),
        new("BL60", "Kunlun", BodyRegion.Leg, BodyView.Back, 27, 438),
        new("KI1", "Yongquan", BodyRegion.Leg, BodyView.Back, 19, 460),
    ];

    /// <summary>Starting point sets the doctor can load and then adjust.</summary>
    public static readonly IReadOnlyList<Protocol> Protocols =
    [
        new("weight", "Weight loss", ["CV12", "ST25", "SP15", "CV6", "ST28", "ST36", "ST40", "SP6"]),
        new("headache", "Headache", ["GV20", "EX-HN5", "GB20", "LI4", "LR3"]),
        new("back", "Low back pain", ["BL23", "BL25", "GV4", "GB30", "BL40", "BL57"]),
        new("sleep", "Insomnia and anxiety", ["GV20", "EX-HN3", "HT7", "PC6", "SP6"]),
        new("digestion", "Digestion", ["CV12", "ST25", "PC6", "ST36", "SP6"]),
    ];

    private static readonly Dictionary<string, Acupoint> ByCode = Points.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

    public static Acupoint? Find(string? code) => code is not null && ByCode.TryGetValue(code, out var p) ? p : null;

    /// <summary>
    /// Diagram x for one side of a point. The front view faces us, so the patient's right is on our left;
    /// the back view is the other way round.
    /// </summary>
    public static double X(Acupoint point, PointSide side, BodyView view) => side switch
    {
        PointSide.Midline => Midline,
        PointSide.Right => view == BodyView.Front ? Midline - point.Dx : Midline + point.Dx,
        _ => view == BodyView.Front ? Midline + point.Dx : Midline - point.Dx,
    };

    /// <summary>Which side of the patient a spot on the diagram is on.</summary>
    public static PointSide SideOf(double x, BodyView view)
    {
        if (Math.Abs(x - Midline) < 2)
        {
            return PointSide.Midline;
        }
        var viewerLeft = x < Midline;
        return viewerLeft == (view == BodyView.Front) ? PointSide.Right : PointSide.Left;
    }
}
