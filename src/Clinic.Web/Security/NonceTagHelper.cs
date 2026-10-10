using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Clinic.Web.Security;

/// <summary>Gives every script tag in the views this request's CSP nonce, so inline scripts written in Razor keep working.</summary>
[HtmlTargetElement("script")]
public class NonceTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!output.Attributes.ContainsName("nonce"))
        {
            output.Attributes.SetAttribute("nonce", ViewContext.HttpContext.CspNonce());
        }
    }
}
