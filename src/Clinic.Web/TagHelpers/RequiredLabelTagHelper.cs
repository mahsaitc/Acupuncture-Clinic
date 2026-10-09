using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Clinic.Web.TagHelpers;

/// <summary>Marks the label of a field that has an explicit [Required] attribute, so it gets a red asterisk.</summary>
[HtmlTargetElement("label", Attributes = "asp-for")]
public class RequiredLabelTagHelper : TagHelper
{
    // After the built-in label tag helper has written the text.
    public override int Order => 1000;

    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (For.Metadata.ValidatorMetadata.OfType<RequiredAttribute>().Any())
        {
            output.AddClass("required", System.Text.Encodings.Web.HtmlEncoder.Default);
        }
    }
}
