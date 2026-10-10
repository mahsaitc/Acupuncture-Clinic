using System.Threading.RateLimiting;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.RateLimiting;

namespace Clinic.Web.Security;

/// <summary>
/// Limits how often one IP address may post the public forms: password guessing across many accounts,
/// mass sign-ups and booking floods. Account lockout still protects each single account.
/// Paths are matched after the /en prefix is removed, so both languages share one limit.
/// </summary>
public static class RequestLimits
{
    public sealed record Rule(string Path, int Permits, TimeSpan Window);

    public static readonly Rule[] Rules =
    [
        new("/Account/Login", 10, TimeSpan.FromMinutes(5)),
        new("/Account/LoginWith2fa", 10, TimeSpan.FromMinutes(5)),
        new("/Account/ForgotPassword", 5, TimeSpan.FromHours(1)),
        new("/Account/ResetPassword", 10, TimeSpan.FromHours(1)),
        new("/Account/Manage", 10, TimeSpan.FromMinutes(5)),
        new("/Account/Manage/TwoFactor", 10, TimeSpan.FromMinutes(5)),
        new("/Account/Register", 5, TimeSpan.FromHours(1)),
        new("/Account/RegisterDoctor", 3, TimeSpan.FromHours(1)),
        new("/Contact", 10, TimeSpan.FromMinutes(15)),
        new("/Booking", 30, TimeSpan.FromMinutes(10)),
    ];

    public static Rule? Match(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
            ? Rules.FirstOrDefault(r => string.Equals(request.Path.Value?.TrimEnd('/'), r.Path, StringComparison.OrdinalIgnoreCase))
            : null;

    public static IServiceCollection AddClinicRequestLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var enabled = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Security:RateLimiting", true);
                if (!enabled || Match(context.Request) is not Rule rule)
                {
                    return RateLimitPartition.GetNoLimiter("none");
                }
                var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"{rule.Path}|{client}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rule.Permits,
                    Window = rule.Window,
                    QueueLimit = 0,
                });
            });
            options.OnRejected = async (context, token) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                {
                    response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                var english = CulturePath.IsEnglish;
                response.ContentType = "text/html; charset=utf-8";
                await response.WriteAsync(english
                    ? """<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Too many attempts</title></head><body style="font-family:sans-serif;max-width:32rem;margin:4rem auto;padding:0 1rem;line-height:1.8"><h1>Too many attempts</h1><p>Too many requests came from your connection. Please wait a few minutes and try again, or call the clinic.</p><p><a href="/en">Back to the home page</a></p></body></html>"""
                    : """<!DOCTYPE html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>تلاش‌های بیش از حد</title></head><body style="font-family:Tahoma,sans-serif;max-width:32rem;margin:4rem auto;padding:0 1rem;line-height:2"><h1>تلاش‌های بیش از حد</h1><p>از اتصال شما درخواست‌های زیادی رسیده است. لطفاً چند دقیقه صبر کنید و دوباره امتحان کنید، یا با کلینیک تماس بگیرید.</p><p><a href="/">بازگشت به صفحه اصلی</a></p></body></html>""",
                    token);
            };
        });
}
