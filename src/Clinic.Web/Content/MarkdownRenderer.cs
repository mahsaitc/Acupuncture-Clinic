using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Html;

namespace Clinic.Web.Content;

/// <summary>Renders post bodies written in Markdown to safe HTML.</summary>
public class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private readonly HtmlSanitizer _sanitizer = new();

    public MarkdownRenderer()
    {
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("mailto");
    }

    public IHtmlContent Render(string? markdown) =>
        new HtmlString(_sanitizer.Sanitize(Markdown.ToHtml(markdown ?? "", _pipeline)));
}
