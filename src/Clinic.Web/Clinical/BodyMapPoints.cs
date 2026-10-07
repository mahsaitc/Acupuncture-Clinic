using System.Text.Json;
using Clinic.Application.Acupuncture;
using Clinic.Domain.Entities;

namespace Clinic.Web.Clinical;

/// <summary>Moves session points between the chart editor's hidden JSON field and the database.</summary>
public static class BodyMapPoints
{
    public const int MaxPoints = 120;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record Dto(string? Code, string? Label, string? Side, string? View, double? X, double? Y, string? Note);

    public static string ToJson(IEnumerable<SessionPoint> points) => JsonSerializer.Serialize(
        points.Select(p => new Dto(p.Code, p.Label, p.Side.ToString(), p.View, p.X, p.Y, p.Note)), Json);

    /// <summary>
    /// Parses what the browser sent. A known code is stored by code and side only, because the library
    /// knows where it goes. Anything else is a hand-placed point: it needs a chart and a position, the
    /// position is clamped to the chart, and on a symmetric chart the side is worked out from the position.
    /// Returns null if the JSON is unusable.
    /// </summary>
    public static List<SessionPoint>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        List<Dto?>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<Dto?>>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
        if (items is null || items.Count > MaxPoints)
        {
            return null;
        }

        var points = new List<SessionPoint>();
        foreach (var item in items)
        {
            if (item is null)
            {
                return null;
            }
            var side = Enum.TryParse<PointSide>(item.Side, true, out var s) && Enum.IsDefined(s) ? s : PointSide.Both;
            var label = (item.Label ?? "").Trim();
            var known = AcupointLibrary.Find(item.Code);
            if (known is not null)
            {
                // The same point twice (say right, then left) becomes one entry for both sides.
                var existing = points.FirstOrDefault(p => p.Code == known.Code);
                side = known.Midline ? PointSide.Midline : side == PointSide.Midline ? PointSide.Both : side;
                if (existing is not null)
                {
                    existing.Side = existing.Side == side ? side : PointSide.Both;
                    continue;
                }
                points.Add(new SessionPoint
                {
                    Code = known.Code,
                    Label = Trim(label.Length == 0 ? known.Label : label, 100)!,
                    Side = side,
                    Note = Trim(item.Note, 200),
                });
                continue;
            }

            var view = AcupointLibrary.FindView(item.View);
            if (view is null || item.X is not { } rawX || item.Y is not { } rawY || !double.IsFinite(rawX) || !double.IsFinite(rawY))
            {
                return null;
            }
            var x = Math.Clamp(Math.Round(rawX, 1), 0, view.Width);
            var y = Math.Clamp(Math.Round(rawY, 1), 0, view.Height);
            points.Add(new SessionPoint
            {
                Label = Trim(label.Length == 0 ? "?" : label, 100)!,
                View = view.Key,
                X = x,
                Y = y,
                Side = view.Symmetric ? AcupointLibrary.SideOf(x, view) : side,
                Note = Trim(item.Note, 200),
            });
        }
        return points;
    }

    /// <summary>The charts, the library and the protocols, for the editor's script.</summary>
    public static string LibraryJson(Func<string, string> translate) => JsonSerializer.Serialize(new
    {
        views = AcupointLibrary.Views.Select(v => new { v.Key, v.Page, v.Width, v.Height, v.Symmetric, v.FacesViewer }),
        points = AcupointLibrary.Points.Select(p => new
        {
            p.Code,
            p.Label,
            p.Midline,
            places = p.Placements.Select(pl => new { pl.View, pl.X, pl.Y }),
        }),
        protocols = AcupointLibrary.Protocols.Select(p => new { p.Key, name = translate(p.Name), codes = p.Codes }),
    }, Json);

    private static string? Trim(string? value, int max)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value.Length > max ? value[..max] : value;
    }
}
