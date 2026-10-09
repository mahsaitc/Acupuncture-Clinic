using Clinic.Domain.Entities;

namespace Clinic.Application.Acupuncture;

/// <summary>
/// One drawing of the point charts. On a symmetric chart (front, back, face) placements are stored as the
/// distance from the midline, and a point on both sides shows twice. FacesViewer means the patient's right
/// is on the viewer's left.
/// </summary>
public sealed record ChartView(string Key, string Page, double Width, double Height, bool Symmetric, bool FacesViewer, string Extension = "svg")
{
    public double Midline => Width / 2;

    public string Image => $"/img/charts/{Key}.{Extension}?v={AcupointLibrary.ChartsVersion}";
}

/// <summary>Where a point sits on one chart. X is the distance from the midline on symmetric charts.</summary>
public sealed record Placement(string View, double X, double Y);

public sealed record Acupoint(string Code, string Name, string Meridian, bool Midline, IReadOnlyList<Placement> Placements)
{
    public bool Bilateral => !Midline;

    public string Label => $"{Code} {Name}";

    /// <summary>The chart the editor opens when this point is added.</summary>
    public string MainView => Placements[0].View;
}

public sealed record Protocol(string Key, string Name, IReadOnlyList<string> Codes);

public readonly record struct Marker(double X, double Y, PointSide Side);

/// <summary>
/// Commonly used points and where they sit on the clinic's charts. The data comes from
/// tools/charts/points.py; the charts are a guide for recording, not an anatomical reference.
/// </summary>
public static partial class AcupointLibrary
{
    public static IReadOnlyList<ChartView> Views => ViewData;

    /// <summary>Chart pages in display order: body, head, arm, leg, ear.</summary>
    public static readonly IReadOnlyList<string> Pages;

    public static IReadOnlyList<Acupoint> Points => PointData;

    /// <summary>Starting point sets the doctor can load and then adjust.</summary>
    public static IReadOnlyList<Protocol> Protocols => ProtocolData;

    /// <summary>
    /// The 361 standard channel points in the conventional order (LU, LI, ST, SP, HT, SI, BL, KI, PC, SJ,
    /// GB, LR, GV, CV, each by number), then the extra and ear points, which have no number.
    /// </summary>
    public static readonly IReadOnlyList<Acupoint> Ordered;

    private static readonly Dictionary<string, int> Numbers;

    private static readonly string[] ChannelOrder = ["LU", "LI", "ST", "SP", "HT", "SI", "BL", "KI", "PC", "SJ", "GB", "LR", "GV", "CV"];

    private static readonly Dictionary<string, Acupoint> ByCode;
    private static readonly Dictionary<string, ChartView> ByKey;

    // In the constructor because the data fields live in the generated file, and field initialisers
    // in different files of a partial class run in no guaranteed order.
    static AcupointLibrary()
    {
        Pages = ViewData.Select(v => v.Page).Distinct().ToArray();
        ByCode = PointData.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        ByKey = ViewData.ToDictionary(v => v.Key, StringComparer.OrdinalIgnoreCase);

        var channel = PointData
            .Where(p => Array.IndexOf(ChannelOrder, p.Meridian) >= 0)
            .OrderBy(p => Array.IndexOf(ChannelOrder, p.Meridian))
            .ThenBy(p => int.Parse(p.Code[p.Meridian.Length..], System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        Numbers = channel.Select((p, i) => (p.Code, i + 1)).ToDictionary(x => x.Code, x => x.Item2, StringComparer.OrdinalIgnoreCase);
        Ordered = [.. channel, .. PointData.Where(p => !Numbers.ContainsKey(p.Code))];
    }

    /// <summary>The point's place, 1 to 361, among the standard channel points; null for extra and ear points.</summary>
    public static int? NumberOf(string? code) => code is not null && Numbers.TryGetValue(code.Trim(), out var n) ? n : null;

    public static Acupoint? Find(string? code) => code is not null && ByCode.TryGetValue(code.Trim(), out var p) ? p : null;

    public static ChartView? FindView(string? key) => key is not null && ByKey.TryGetValue(key, out var v) ? v : null;

    /// <summary>Chart x of one side of a symmetric placement.</summary>
    public static double X(ChartView view, double dx, PointSide side) => side switch
    {
        PointSide.Midline => view.Midline,
        PointSide.Right => view.FacesViewer ? view.Midline - dx : view.Midline + dx,
        _ => view.FacesViewer ? view.Midline + dx : view.Midline - dx,
    };

    /// <summary>Which side of the patient a spot on a symmetric chart is on.</summary>
    public static PointSide SideOf(double x, ChartView view)
    {
        if (Math.Abs(x - view.Midline) < 2)
        {
            return PointSide.Midline;
        }
        var viewerLeft = x < view.Midline;
        return viewerLeft == view.FacesViewer ? PointSide.Right : PointSide.Left;
    }

    /// <summary>
    /// Where a recorded point shows on one chart. A library point shows on every chart it has a placement on,
    /// once per side on symmetric charts; a point the doctor placed by hand shows only where it was placed.
    /// </summary>
    public static IEnumerable<Marker> Markers(SessionPoint point, ChartView view)
    {
        var known = Find(point.Code);
        if (known is null)
        {
            if (point.X is { } x && point.Y is { } y && string.Equals(point.View, view.Key, StringComparison.OrdinalIgnoreCase))
            {
                yield return new Marker(x, y, point.Side);
            }
            yield break;
        }

        var place = known.Placements.FirstOrDefault(p => p.View == view.Key);
        if (place is null)
        {
            yield break;
        }
        if (!view.Symmetric)
        {
            yield return new Marker(place.X, place.Y, point.Side);
            yield break;
        }
        PointSide[] sides = known.Midline || place.X == 0 ? [PointSide.Midline]
            : point.Side == PointSide.Both || point.Side == PointSide.Midline ? [PointSide.Right, PointSide.Left]
            : [point.Side];
        foreach (var side in sides)
        {
            yield return new Marker(X(view, place.X, side), place.Y, side);
        }
    }
}
