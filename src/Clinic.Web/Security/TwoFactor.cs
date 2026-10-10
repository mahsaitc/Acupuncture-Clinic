using System.Security.Claims;
using System.Text;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QRCoder;

namespace Clinic.Web.Security;

/// <summary>
/// Two-step login with an authenticator app (Google Authenticator, Microsoft Authenticator and the like): after the
/// password, a 6-digit code from the phone. Required for every staff account when "Security:RequireStaffTwoFactor" is on
/// (the default): staff who have not set it up are sent to the setup page before the management panel opens.
/// </summary>
public static class TwoFactor
{
    public static bool RequiredForStaff(IConfiguration config) => config.GetValue("Security:RequireStaffTwoFactor", true);

    public static bool IsStaff(ClaimsPrincipal user) => PasswordPolicy.StaffRoles.Any(user.IsInRole);

    /// <summary>The key in groups of four, for typing into the app when the QR code cannot be scanned.</summary>
    public static string FormatKey(string key)
    {
        var text = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            text.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        }
        return text.ToString().TrimEnd().ToLowerInvariant();
    }

    public static string AuthenticatorUri(string issuer, string account, string key) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

    /// <summary>The QR code as inline SVG, drawn on the server so no outside service ever sees the key.</summary>
    public static string QrSvg(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(5, "#000000", "#ffffff", drawQuietZones: true, sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}

/// <summary>Sends staff without two-step login to its setup page instead of the management panel.</summary>
public sealed class RequireTwoFactorFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated == true
            && TwoFactor.IsStaff(http.User)
            && TwoFactor.RequiredForStaff(http.RequestServices.GetRequiredService<IConfiguration>()))
        {
            var users = http.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.GetUserAsync(http.User);
            if (user is not null && !user.TwoFactorEnabled)
            {
                context.Result = new RedirectToPageResult("/Account/Manage/TwoFactor", new { required = true });
                return;
            }
        }
        await next();
    }
}
