using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Clinic.Domain;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

/// <summary>A site that requires two-step login for staff, as in production, and keeps the emails it would send.</summary>
public sealed class StaffSecurityWebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clinic-2fa-{Guid.NewGuid():N}.db");
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"clinic-2fa-{Guid.NewGuid():N}");

    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Clinic", $"Data Source={_dbPath}");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(_dataDir, "keys"));
        builder.UseSetting("PrivateFiles:Root", Path.Combine(_dataDir, "private"));
        builder.UseSetting("Media:Root", Path.Combine(_dataDir, "media"));
        builder.UseSetting("Security:RateLimiting", "false");
        builder.UseSetting("Security:BotCheck", "false");
        builder.ConfigureTestServices(services => services.AddSingleton<EmailSender>(sp => new RecordingEmailSender(this, sp)));
    }

    private sealed class RecordingEmailSender(StaffSecurityWebFactory factory, IServiceProvider sp) : EmailSender(
        sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
        sp.GetRequiredService<IWebHostEnvironment>(),
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EmailSender>>())
    {
        public override Task SendAsync(string to, string subject, string body)
        {
            lock (factory.Sent)
            {
                factory.Sent.Add((to, subject, body));
            }
            return Task.CompletedTask;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }
}

public partial class AccountSecurityTests(StaffSecurityWebFactory factory) : IClassFixture<StaffSecurityWebFactory>
{
    private const string Password = "Clinic-Passw0rd-2026";

    [Fact]
    public async Task Staff_without_two_step_login_are_sent_to_set_it_up()
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Receptionist));

        var response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Manage/TwoFactor?required=True", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Patients_are_not_asked_for_two_step_login()
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Patient));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Appointments/Mine")).StatusCode);
    }

    [Fact]
    public async Task Doctor_sets_up_two_step_login_and_then_logs_in_with_the_app_code()
    {
        var doctor = await CreateUserAsync(Roles.Doctor);
        var client = await LoginAsync(doctor);

        var setup = await client.GetStringAsync("/Account/Manage/TwoFactor?required=true");
        Assert.Contains("<svg", setup);
        var key = KeyPattern().Match(setup).Groups[1].Value.Replace(" ", "");
        var enabled = await PostFormAsync(client, "/Account/Manage/TwoFactor", new() { ["Code"] = Totp(key) });
        var html = await enabled.Content.ReadAsStringAsync();
        Assert.Contains("کدهای بازیابی را ذخیره کنید", html);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);

        // A new login asks for the code after the password.
        var fresh = NewClient();
        var afterPassword = await PostFormAsync(fresh, "/Account/Login", new() { ["Input.Email"] = doctor.Email!, ["Input.Password"] = Password });
        Assert.Equal(HttpStatusCode.Redirect, afterPassword.StatusCode);
        Assert.Contains("/Account/LoginWith2fa", afterPassword.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await fresh.GetAsync("/Admin")).StatusCode);

        var wrong = await PostFormAsync(fresh, "/Account/LoginWith2fa", new() { ["Code"] = "000000" == Totp(key) ? "111111" : "000000" });
        Assert.Contains("کد درست نیست", await wrong.Content.ReadAsStringAsync());
        var right = await PostFormAsync(fresh, "/Account/LoginWith2fa", new() { ["Code"] = Totp(key) });
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_logs_in_once_instead_of_the_app()
    {
        var admin = await CreateUserAsync(Roles.Admin);
        var codes = await EnableTwoFactorAsync(admin);

        var client = NewClient();
        await PostFormAsync(client, "/Account/Login", new() { ["Input.Email"] = admin.Email!, ["Input.Password"] = Password });
        var used = await PostFormAsync(client, "/Account/LoginWith2fa?recovery=true", new() { ["Code"] = codes[0] });
        Assert.Equal(HttpStatusCode.Redirect, used.StatusCode);

        var again = NewClient();
        await PostFormAsync(again, "/Account/Login", new() { ["Input.Email"] = admin.Email!, ["Input.Password"] = Password });
        var reused = await PostFormAsync(again, "/Account/LoginWith2fa?recovery=true", new() { ["Code"] = codes[0] });
        Assert.Equal(HttpStatusCode.OK, reused.StatusCode);
    }

    [Fact]
    public async Task Admin_turns_off_two_step_login_of_someone_who_lost_their_phone()
    {
        var admin = await CreateUserAsync(Roles.Admin);
        await EnableTwoFactorAsync(admin);
        var doctor = await CreateUserAsync(Roles.Doctor);
        await EnableTwoFactorAsync(doctor);
        var client = await LoginAsync(admin);

        // Without the phone, the doctor guesses codes until the account locks.
        var guessing = NewClient();
        await PostFormAsync(guessing, "/Account/Login", new() { ["Input.Email"] = doctor.Email!, ["Input.Password"] = Password });
        for (var i = 0; i < 5; i++)
        {
            await PostFormAsync(guessing, "/Account/LoginWith2fa", new() { ["Code"] = "000000" });
        }

        await PostFormAsync(client, $"/Admin/Users?handler=ResetTwoFactor&id={doctor.Id}", [], tokenFrom: "/Admin/Users");

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.False((await users.FindByIdAsync(doctor.Id))!.TwoFactorEnabled);

        // The doctor logs in with the old password and is sent to set up the new phone.
        var doctorClient = NewClient();
        var login = await PostFormAsync(doctorClient, "/Account/Login", new() { ["Input.Email"] = doctor.Email!, ["Input.Password"] = Password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.DoesNotContain("LoginWith2fa", login.Headers.Location!.OriginalString);
        var panel = await doctorClient.GetAsync("/Admin");
        Assert.Contains("/Account/Manage/TwoFactor", panel.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Forgotten_password_is_reset_from_the_emailed_link()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var client = NewClient();

        var asked = await PostFormAsync(client, "/Account/ForgotPassword", new() { ["Email"] = patient.Email! });
        Assert.Contains("اگر حسابی با این ایمیل وجود داشته باشد", await asked.Content.ReadAsStringAsync());
        var mail = factory.Sent.Single(m => m.To == patient.Email);
        var link = new Uri(LinkPattern().Match(mail.Body).Value);

        const string newPassword = "Brand-New-Pass-77";
        var reset = await PostFormAsync(client, link.PathAndQuery, new()
        {
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = newPassword,
        });
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);

        var login = await PostFormAsync(NewClient(), "/Account/Login", new() { ["Input.Email"] = patient.Email!, ["Input.Password"] = newPassword });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        // The link works once.
        var reused = await PostFormAsync(client, link.PathAndQuery, new() { ["NewPassword"] = "Another-Pass-88", ["ConfirmPassword"] = "Another-Pass-88" });
        Assert.Contains("منقضی شده یا قبلاً استفاده شده", await reused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_answer_and_no_mail()
    {
        var before = factory.Sent.Count;

        var asked = await PostFormAsync(NewClient(), "/Account/ForgotPassword", new() { ["Email"] = $"nobody-{Guid.NewGuid():N}@example.com" });

        Assert.Contains("اگر حسابی با این ایمیل وجود داشته باشد", await asked.Content.ReadAsStringAsync());
        Assert.Equal(before, factory.Sent.Count);
    }

    [Theory]
    [InlineData("Password123", "رایج")]
    [InlineData("Qwerty1234", "رایج")]
    public async Task Common_passwords_are_refused_at_sign_up(string password, string message)
    {
        var email = $"weak-{Guid.NewGuid():N}@example.com";
        var response = await PostFormAsync(NewClient(), "/Account/Register", new()
        {
            ["Input.FullName"] = "بیمار",
            ["Input.Email"] = email,
            ["Input.PhoneNumber"] = "09" + Random.Shared.Next(100000000, 999999999),
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Staff_need_longer_passwords_than_patients()
    {
        var l = new PassThroughLocalizer();

        Assert.Empty(PasswordPolicy.Check("Sahar2026x", staff: false, "sara@example.com", "09121234567", l));
        Assert.NotEmpty(PasswordPolicy.Check("Sahar2026x", staff: true, "sara@example.com", "09121234567", l));
        Assert.Empty(PasswordPolicy.Check("Sahar-Clinic-2026", staff: true, "sara@example.com", "09121234567", l));
        Assert.NotEmpty(PasswordPolicy.Check("Sara-09121234567", staff: true, "sara@example.com", "09121234567", l));
    }

    private sealed class PassThroughLocalizer : Microsoft.Extensions.Localization.IStringLocalizer
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] => new(name, name);
        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<ApplicationUser> CreateUserAsync(string role)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.example";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = $"{role} test" };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRoleAsync(user, role);
        return user;
    }

    /// <summary>Turns two-step login on directly and returns the recovery codes.</summary>
    private async Task<string[]> EnableTwoFactorAsync(ApplicationUser user)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await users.FindByIdAsync(user.Id))!;
        await users.ResetAuthenticatorKeyAsync(stored);
        await users.SetTwoFactorEnabledAsync(stored, true);
        return (await users.GenerateNewTwoFactorRecoveryCodesAsync(stored, 10))!.ToArray();
    }

    /// <summary>Logs in, answering the second step with the app code when two-step login is on.</summary>
    private async Task<HttpClient> LoginAsync(ApplicationUser user)
    {
        var client = NewClient();
        var response = await PostFormAsync(client, "/Account/Login", new() { ["Input.Email"] = user.Email!, ["Input.Password"] = Password });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        if (response.Headers.Location!.OriginalString.Contains("LoginWith2fa"))
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var key = (await users.GetAuthenticatorKeyAsync((await users.FindByIdAsync(user.Id))!))!;
            var second = await PostFormAsync(client, "/Account/LoginWith2fa", new() { ["Code"] = Totp(key) });
            Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        }
        return client;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> fields, string? tokenFrom = null)
    {
        var page = await client.GetAsync(tokenFrom ?? url);
        var html = await page.Content.ReadAsStringAsync();
        fields["__RequestVerificationToken"] = TokenPattern().Match(html).Groups[1].Value;
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    /// <summary>The current 6-digit code of an authenticator app (RFC 6238, 30-second steps, SHA-1).</summary>
    private static string Totp(string base32Key)
    {
        var key = Base32Decode(base32Key);
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string text)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in text.Trim('=').ToUpperInvariant())
        {
            value = (value << 5) | Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. output];
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("<code[^>]*>([a-z2-7 ]+)</code>")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}
