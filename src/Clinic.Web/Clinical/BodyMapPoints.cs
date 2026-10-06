using System.Text.Json;
using Clinic.Application.Acupuncture;
using Clinic.Domain.Entities;

namespace Clinic.Web.Clinical;

/// <summary>Moves session points between the body diagram's hidden JSON field and the database.</summary>
public static class BodyMapPoints
{
    public const int MaxPoints = 120;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record Dto(string? Code, string Label, string View, string Side, double X, double Y, string? Note);

    public static string ToJson(IEnumerable<SessionPoint> points) => JsonSerializer.Serialize(
        points.Select(p => new Dto(p.Code, p.Label, p.View.ToString(), p.Side.ToString(), p.X, p.Y, p.Note)), Json);

    /// <summary>
    /// Parses what the browser sent. Coordinates are clamped to the diagram, the side is worked out again
    /// from the position, and an unknown code becomes a free point. Returns null if the JSON is unusable.
    /// </summary>
    public static List<SessionPoint>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        List<Dto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<Dto>>(json, Json);
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
            if (item is null || !Enum.TryParse<BodyView>(item.View, out var view) || !double.IsFinite(item.X) || !double.IsFinite(item.Y))
            {
                return null;
            }
            var x = Math.Clamp(Math.Round(item.X, 1), 0, AcupointLibrary.Width);
            var y = Math.Clamp(Math.Round(item.Y, 1), 0, AcupointLibrary.Height);
            var known = AcupointLibrary.Find(item.Code);
            var label = (item.Label ?? "").Trim();
            if (label.Length == 0)
            {
                label = known?.Label ?? "?";
            }
            points.Add(new SessionPoint
            {
                Code = known?.Code,
                Label = label.Length > 100 ? label[..100] : label,
                View = view,
                Side = AcupointLibrary.SideOf(x, view),
                X = x,
                Y = y,
                Note = Trim(item.Note, 200),
            });
        }
        return points;
    }

    /// <summary>The library and protocols, for the diagram's script.</summary>
    public static string LibraryJson(Func<string, string> translate) => JsonSerializer.Serialize(new
    {
        points = AcupointLibrary.Points.Select(p => new
        {
            p.Code,
            p.Label,
            region = p.Region.ToString(),
            view = p.View.ToString(),
            p.Y,
            right = AcupointLibrary.X(p, p.Bilateral ? PointSide.Right : PointSide.Midline, p.View),
            left = p.Bilateral ? AcupointLibrary.X(p, PointSide.Left, p.View) : (double?)null,
        }),
        protocols = AcupointLibrary.Protocols.Select(p => new { p.Key, name = translate(p.Name), codes = p.Codes }),
    }, Json);

    private static string? Trim(string? value, int max)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value.Length > max ? value[..max] : value;
    }
}
