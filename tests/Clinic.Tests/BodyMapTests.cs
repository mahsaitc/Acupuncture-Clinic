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
    }

    [Fact]
    public void Every_point_lands_on_the_diagram_on_the_side_it_belongs_to()
    {
        foreach (var point in AcupointLibrary.Points)
        {
            Assert.InRange(point.Y, 0, AcupointLibrary.Height);
            var sides = point.Bilateral ? new[] { PointSide.Right, PointSide.Left } : [PointSide.Midline];
            foreach (var side in sides)
            {
                var x = AcupointLibrary.X(point, side, point.View);
                Assert.InRange(x, 0, AcupointLibrary.Width);
                Assert.Equal(side, AcupointLibrary.SideOf(x, point.View));
            }
        }
    }

    [Fact]
    public void Patients_right_is_on_the_viewers_left_from_the_front_only()
    {
        Assert.Equal(PointSide.Right, AcupointLibrary.SideOf(60, BodyView.Front));
        Assert.Equal(PointSide.Left, AcupointLibrary.SideOf(60, BodyView.Back));
        Assert.Equal(PointSide.Midline, AcupointLibrary.SideOf(100.5, BodyView.Back));
    }

    [Fact]
    public void Parse_rejects_broken_or_oversized_input()
    {
        Assert.Null(BodyMapPoints.Parse("{not json"));
        Assert.Null(BodyMapPoints.Parse("""[{"label":"x","view":"Side","x":1,"y":1}]"""));
        var many = "[" + string.Join(",", Enumerable.Repeat("""{"label":"x","view":"Front","x":1,"y":1}""", BodyMapPoints.MaxPoints + 1)) + "]";
        Assert.Null(BodyMapPoints.Parse(many));
        Assert.Empty(BodyMapPoints.Parse("")!);
    }

    [Fact]
    public void Parse_trusts_the_position_not_the_claimed_side()
    {
        var points = BodyMapPoints.Parse("""[{"code":"st36","label":"","view":"Front","side":"Left","x":74,"y":370,"note":"  "}]""")!;

        var p = Assert.Single(points);
        Assert.Equal("ST36", p.Code);
        Assert.Equal("ST36 Zusanli", p.Label);
        Assert.Equal(PointSide.Right, p.Side);
        Assert.Null(p.Note);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var original = new SessionPoint { Code = "LI4", Label = "LI4 Hegu", View = BodyView.Front, Side = PointSide.Left, X = 173, Y = 276, Note = "strong" };

        var back = Assert.Single(BodyMapPoints.Parse(BodyMapPoints.ToJson([original]))!);

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
}
