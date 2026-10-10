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
        // Images in posts load only when scrolled to, so long articles open fast.
        _sanitizer.AllowedAttributes.Add("loading");
        _sanitizer.AllowedAttributes.Add("decoding");
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement { LocalName: "img" } image)
            {
                image.SetAttribute("loading", "lazy");
                image.SetAttribute("decoding", "async");
            }
        };
    }

    public IHtmlContent Render(string? markdown) =>
        new HtmlString(_sanitizer.Sanitize(Markdown.ToHtml(markdown ?? "", _pipeline)));
}
