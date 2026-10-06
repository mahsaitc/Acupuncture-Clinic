using Clinic.Web.Content;

namespace Clinic.Tests;

public class ContentTests
{
    [Fact]
    public void Map_embed_accepts_the_whole_iframe_code()
    {
        const string code = """<iframe src="https://www.google.com/maps/embed?pb=!1m18!1m12&amp;x=1" width="600" height="450" style="border:0;" allowfullscreen="" loading="lazy"></iframe>""";

        Assert.Equal("https://www.google.com/maps/embed?pb=!1m18!1m12&x=1", MapEmbed.ExtractSrc(code));
    }

    [Theory]
    [InlineData("https://www.google.com/maps/embed?pb=abc", "https://www.google.com/maps/embed?pb=abc")]
    [InlineData("https://evil.example/maps/embed?pb=abc", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("<iframe src=\"https://www.google.com.evil.example/maps/embed?pb=1\"></iframe>", null)]
    [InlineData("", null)]
    public void Map_embed_only_allows_google_maps(string input, string? expected) =>
        Assert.Equal(expected, MapEmbed.ExtractSrc(input));

    [Theory]
    [InlineData("Acupuncture for Back Pain!", "acupuncture-for-back-pain")]
    [InlineData("طب سوزنی و کمردرد", "طب-سوزنی-و-کمردرد")]
    [InlineData("  ../../etc/passwd  ", "etc-passwd")]
    public void Slug_keeps_letters_and_digits(string title, string expected) =>
        Assert.Equal(expected, Slug.From(title));

    [Fact]
    public void Markdown_renders_formatting_but_not_raw_html_or_scripts()
    {
        var html = new MarkdownRenderer().Render("## عنوان\n\n**پررنگ** <script>alert(1)</script> [x](javascript:alert(1))").ToString()!;

        Assert.Contains("<h2", html);
        Assert.Contains("<strong>پررنگ</strong>", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("javascript:", html);
    }
}
