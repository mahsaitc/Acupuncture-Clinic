using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Security;

/// <summary>
/// An invisible check against sign-up and message bots, with no outside service (reCAPTCHA is unreliable in Iran):
/// the form carries a signed time stamp and a hidden field people never see. A post that comes back faster than a
/// person could type, without the time stamp, or with the hidden field filled is refused.
/// Rendered by the _BotCheck partial; enforced by <see cref="BotCheckAttribute"/> on the page.
/// </summary>
public class BotCheck(IDataProtectionProvider protection, TimeProvider time, IConfiguration config)
{
    public const string StampField = "__FormStamp";
    public const string TrapField = "__HomePage";

    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(12);
    private readonly IDataProtector _protector = protection.CreateProtector("Clinic.BotCheck");

    public bool Enabled => config.GetValue("Security:BotCheck", true);
    private TimeSpan MinAge => TimeSpan.FromSeconds(config.GetValue("Security:BotCheckMinSeconds", 3));

    public string NewStamp() => _protector.Protect(time.GetUtcNow().UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public bool LooksHuman(IFormCollection form)
    {
        if (!Enabled)
        {
            return true;
        }
        if (!string.IsNullOrEmpty(form[TrapField]))
        {
            return false;
        }
        try
        {
            var ticks = long.Parse(_protector.Unprotect(form[StampField].ToString()), System.Globalization.CultureInfo.InvariantCulture);
            var age = time.GetUtcNow().UtcDateTime - new DateTime(ticks, DateTimeKind.Utc);
            return age >= MinAge && age <= MaxAge;
        }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException or ArgumentException or OverflowException)
        {
            return false;
        }
    }
}

/// <summary>Refuses a form post that <see cref="BotCheck"/> judges automated; the page shows the form again with a message.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BotCheckAttribute : Attribute, IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsPost(request.Method) && request.HasFormContentType)
        {
            var services = context.HttpContext.RequestServices;
            var check = services.GetRequiredService<BotCheck>();
            if (!check.LooksHuman(await request.ReadFormAsync()))
            {
                var l = services.GetRequiredService<IStringLocalizer<SharedResource>>();
                context.ModelState.AddModelError(string.Empty, l["The form was sent too quickly or could not be verified. Please wait a few seconds and send it again."]);
            }
        }
        await next();
    }
}
