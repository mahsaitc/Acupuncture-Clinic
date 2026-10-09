using Clinic.Application.Acupuncture;
using Clinic.Application.Common;
using Clinic.Domain.Entities;
using Clinic.Web.Clinical;

namespace Clinic.Tests;

public class BodyMapTests
{
    [Fact]
    public void Library_codes_are_unique_and_protocols_use_known_points()
    {
        Assert.Equal(AcupointLibrary.Points.Count, AcupointLibrary.Points.Select(p => p.Code).Distinct().Count());
        Assert.All(AcupointLibrary.Protocols.SelectMany(p => p.Codes), code => Assert.NotNull(AcupointLibrary.Find(code)));
        Assert.True(AcupointLibrary.Points.Count > 150);
        Assert.Equal(["body", "head", "arm", "leg", "ear"], AcupointLibrary.Pages);
    }

    [Fact]
    public void Charts_are_the_rendered_photos_except_the_ear()
    {
        // The body, head, arm and leg charts are rendered from Z-Anatomy (tools/blender); only the ear chart is a drawing.
        // If this fails, tools/charts/generate.py was run without the photo inputs: see CLAUDE.md before changing it.
        Assert.All(AcupointLibrary.Views.Where(v => v.Key != "ear"), v => Assert.Equal("png", v.Extension));
        Assert.Equal("svg", AcupointLibrary.FindView("ear")!.Extension);
    }

    [Fact]
    public void Every_point_is_on_a_chart_and_lands_inside_it_on_the_right_side()
    {
        foreach (var point in AcupointLibrary.Points)
        {
            Assert.NotEmpty(point.Placements);
            foreach (var place in point.Placements)
            {
                var view = AcupointLibrary.FindView(place.View);
                Assert.NotNull(view);
                var recorded = new SessionPoint { Code = point.Code, Side = point.Midline ? PointSide.Midline : PointSide.Both };
                var markers = AcupointLibrary.Markers(recorded, view).ToList();
                Assert.Equal(view.Symmetric && place.X > 0 ? 2 : 1, markers.Count);
                foreach (var m in markers)
                {
                    Assert.InRange(m.X, 0, view.Width);
                    Assert.InRange(m.Y, 0, view.Height);
                    if (view.Symmetric)
                    {
                        Assert.Equal(m.Side, AcupointLibrary.SideOf(m.X, view));
                    }
                }
            }
        }
    }

    [Fact]
    public void A_point_chosen_by_name_shows_on_every_chart_it_belongs_to()
    {
        var st36 = new SessionPoint { Code = "ST36", Side = PointSide.Right };

        var shownOn = AcupointLibrary.Views.Where(v => AcupointLibrary.Markers(st36, v).Any()).Select(v => v.Key);

        Assert.Equal(["front", "side", "leg-outer"], shownOn);
        // From the front the patient's right leg is on the viewer's left.
        Assert.True(Assert.Single(AcupointLibrary.Markers(st36, AcupointLibrary.FindView("front")!)).X < 100);
        var both = new SessionPoint { Code = "BL23", Side = PointSide.Both };
        Assert.Equal(2, AcupointLibrary.Markers(both, AcupointLibrary.FindView("back")!).Count());
    }

    [Fact]
    public void Patients_right_is_on_the_viewers_left_from_the_front_only()
    {
        Assert.Equal(PointSide.Right, AcupointLibrary.SideOf(60, AcupointLibrary.FindView("front")!));
        Assert.Equal(PointSide.Left, AcupointLibrary.SideOf(60, AcupointLibrary.FindView("back")!));
        Assert.Equal(PointSide.Right, AcupointLibrary.SideOf(100, AcupointLibrary.FindView("head-front")!));
        Assert.Equal(PointSide.Midline, AcupointLibrary.SideOf(100.5, AcupointLibrary.FindView("back")!));
    }

    [Fact]
    public void Parse_rejects_broken_or_oversized_input()
    {
        Assert.Null(BodyMapPoints.Parse("{not json"));
        Assert.Null(BodyMapPoints.Parse("""[{"label":"x","view":"top","x":1,"y":1}]"""));
        Assert.Null(BodyMapPoints.Parse("""[{"label":"x","view":"front"}]"""));
        var many = "[" + string.Join(",", Enumerable.Repeat("""{"label":"x","view":"front","x":1,"y":1}""", BodyMapPoints.MaxPoints + 1)) + "]";
        Assert.Null(BodyMapPoints.Parse(many));
        Assert.Empty(BodyMapPoints.Parse("")!);
    }

    [Fact]
    public void Parse_stores_a_standard_point_by_name_and_merges_its_sides()
    {
        var points = BodyMapPoints.Parse("""
            [{"code":"st36","label":"","side":"Right","view":"front","x":3,"y":4,"note":"  "},
             {"code":"ST36","side":"Left"},
             {"code":"CV12","side":"Left"}]
            """)!;

        Assert.Collection(points,
            p =>
            {
                Assert.Equal("ST36", p.Code);
                Assert.Equal("ST36 Zusanli", p.Label);
                Assert.Equal(PointSide.Both, p.Side);
                Assert.Null(p.View);
                Assert.Null(p.X);
                Assert.Null(p.Note);
            },
            p => { Assert.Equal("CV12", p.Code); Assert.Equal(PointSide.Midline, p.Side); });
    }

    [Fact]
    public void Parse_trusts_the_position_of_a_hand_placed_point_not_the_claimed_side()
    {
        var points = BodyMapPoints.Parse("""
            [{"label":"Ashi","view":"FRONT","side":"Left","x":60,"y":900},
             {"label":"Ear seed","view":"ear","side":"Left","x":100,"y":100}]
            """)!;

        Assert.Collection(points,
            p => { Assert.Equal("front", p.View); Assert.Equal(PointSide.Right, p.Side); Assert.Equal(480, p.Y); },
            p => { Assert.Equal("ear", p.View); Assert.Equal(PointSide.Left, p.Side); });
    }

    [Fact]
    public void Round_trips_through_json()
    {
        SessionPoint[] original =
        [
            new() { Code = "LI4", Label = "LI4 Hegu", Side = PointSide.Left, Note = "strong" },
            new() { Label = "Scar", View = "back", Side = PointSide.Left, X = 80, Y = 200 },
        ];

        var back = BodyMapPoints.Parse(BodyMapPoints.ToJson(original))!;

        Assert.Equivalent(original, back);
    }

    [Theory]
    [InlineData("1405/07/14", 2026, 10, 6)]
    [InlineData("۱۳۶۵/۰۴/۱۲", 1986, 7, 3)]
    [InlineData("1403-12-30", 2025, 3, 20)]
    public void Jalali_dates_parse(string text, int y, int m, int d)
    {
        Assert.True(JalaliDate.TryParse(text, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Theory]
    [InlineData("1405/13/01")]
    [InlineData("1404/12/30")]
    [InlineData("2026-10-06")]
    [InlineData("hello")]
    public void Invalid_jalali_dates_are_rejected(string text) => Assert.False(JalaliDate.TryParse(text, out _));

    [Fact]
    public void Private_store_recognises_pdf_and_dicom()
    {
        var dicom = new byte[132];
        "DICM"u8.CopyTo(dicom.AsSpan(128));

        Assert.True(PrivateFileStore.LooksLike("application/pdf", "%PDF-1.4"u8));
        Assert.False(PrivateFileStore.LooksLike("application/pdf", "<html>"u8));
        Assert.True(PrivateFileStore.LooksLike("application/dicom", dicom));
        Assert.False(PrivateFileStore.LooksLike("application/dicom", new byte[132]));
    }

    [Fact]
    public void Channel_points_are_numbered_1_to_361_in_the_usual_order()
    {
        Assert.Equal(1, AcupointLibrary.NumberOf("LU1"));
        Assert.Equal(67, AcupointLibrary.NumberOf("ST36"));
        Assert.Equal(361, AcupointLibrary.NumberOf("CV24"));
        Assert.Null(AcupointLibrary.NumberOf("EX-HN3"));
        Assert.Equal(Enumerable.Range(1, 361), AcupointLibrary.Ordered.Take(361).Select(p => AcupointLibrary.NumberOf(p.Code)!.Value));
    }

    [Fact]
    public void Every_body_point_is_also_on_a_whole_body_chart()
    {
        var body = new[] { "front", "back", "side" };
        var missing = AcupointLibrary.Points
            .Where(p => p.Meridian != "EAR" && !p.Placements.Any(pl => body.Contains(pl.View)))
            .Select(p => p.Code).ToList();
        Assert.Empty(missing);
    }
}
