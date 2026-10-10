using System.Net;
using System.Text.RegularExpressions;
using Clinic.Domain;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Branding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

/// <summary>A site whose configuration names its owner, as on the owner's own server.</summary>
public sealed class OwnerWebFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@clinic.test";
    public const string OwnerPassword = "Owner1234!";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clinic-owner-{Guid.NewGuid():N}.db");
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"clinic-owner-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Clinic", $"Data Source={_dbPath}");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(_dataDir, "keys"));
        builder.UseSetting("PrivateFiles:Root", Path.Combine(_dataDir, "private"));
        builder.UseSetting("Media:Root", Path.Combine(_dataDir, "media"));
        builder.UseSetting("Owner:Email", OwnerEmail);
        builder.UseSetting("Owner:Password", OwnerPassword);
        builder.UseSetting("Owner:Name", "مالک تست");
        builder.UseSetting("Security:RateLimiting", "false");
        builder.UseSetting("Security:BotCheck", "false");
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

public partial class BrandingTests(OwnerWebFactory factory) : IClassFixture<OwnerWebFactory>
{
    private const string Password = "Passw0rd!";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Fact]
    public void Default_colours_write_no_theme_and_a_new_colour_keeps_the_olive_scale()
    {
        Assert.Null(ThemePalette.Css(null, null));
        Assert.Null(ThemePalette.Css("#5D7139", ThemePalette.DefaultAccent));
        Assert.Null(ThemePalette.Css("not a colour", "#12345"));

        var css = ThemePalette.Css("#2f5f9e", null)!;
        Assert.StartsWith(":root{", css);
        Assert.Contains("--olive-600:#2f5f9e;", css);
        Assert.Contains("--bs-success-rgb:", css);
        Assert.DoesNotContain("--sand-500", css);
        Assert.Contains("--sand-500:", ThemePalette.Css(null, "#d9a441"));
    }

    [Fact]
    public async Task Only_the_configured_owner_holds_the_owner_role_and_the_admin_cannot_touch_it()
    {
        var owner = await OwnerAsync();
        var admin = await CreateUserAsync(Roles.Admin);
        await AddRoleAsync(admin, Roles.Owner);

        // At the next start-up the configuration decides again: the role is taken from anyone else.
        await using (var restarted = factory.WithWebHostBuilder(_ => { }))
        {
            restarted.CreateClient();
            using var scope = restarted.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var holders = await userManager.GetUsersInRoleAsync(Roles.Owner);
            Assert.Equal(owner.Id, Assert.Single(holders).Id);
        }

        var adminClient = await LoginAsync(admin.Email!, Password);
        Assert.StartsWith("/Account/AccessDenied", (await adminClient.GetAsync("/Admin/Branding")).Headers.Location!.PathAndQuery);
        Assert.DoesNotContain("/Admin/Branding", await adminClient.GetStringAsync("/Admin"));

        var usersPage = await adminClient.GetStringAsync("/Admin/Users?Q=" + Uri.EscapeDataString(OwnerWebFactory.OwnerEmail));
        Assert.Contains("مالک سایت", usersPage);
        Assert.DoesNotContain("handler=ToggleRole", usersPage);

        // Even a crafted post changes nothing.
        foreach (var url in new[] { $"/Admin/Users?handler=ToggleRole&id={owner.Id}&role={Roles.Admin}", $"/Admin/Users?handler=ToggleActive&id={owner.Id}" })
        {
            await PostFormAsync(adminClient, url, "/Admin/Users", new());
        }
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var fresh = (await userManager.FindByIdAsync(owner.Id))!;
            Assert.True(fresh.IsActive);
            Assert.True(await userManager.IsInRoleAsync(fresh, Roles.Admin));
        }

        // The admin's new-user form never offers the owner role.
        Assert.DoesNotContain($"value=\"{Roles.Owner}\"", await adminClient.GetStringAsync("/Admin/UserCreate"));
    }

    [Fact]
    public async Task The_owner_changes_name_logo_title_and_colours_everywhere()
    {
        await OwnerAsync();
        var client = await LoginAsync(OwnerWebFactory.OwnerEmail, OwnerWebFactory.OwnerPassword);
        var page = await client.GetStringAsync("/Admin/Branding");
        Assert.Contains("لوگو، نام و رنگ‌ها", page);
        Assert.Contains("/Admin/Branding", await client.GetStringAsync("/Admin"));

        var token = TokenPattern().Match(page).Groups[1].Value;
        using (var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("کلینیک نمونه دکتر رضایی"), "Input.ClinicNameFa" },
            { new StringContent("Dr. Rezaei Sample Clinic"), "Input.ClinicNameEn" },
            { new StringContent("کلینیک نمونه"), "Input.BrandTitleFa" },
            { new StringContent("Sample Clinic"), "Input.BrandTitleEn" },
            { new StringContent("دکتر رضایی"), "Input.BrandSubtitleFa" },
            { new StringContent("Dr. Rezaei"), "Input.BrandSubtitleEn" },
            { new StringContent("به کلینیک نمونه خوش آمدید"), "Input.HeroTitleFa" },
            { new StringContent("Welcome to the sample clinic"), "Input.HeroTitleEn" },
            { new StringContent("#2F5F9E"), "Input.ThemeColor" },
            { new StringContent(ThemePalette.DefaultAccent), "Input.AccentColor" },
        })
        {
            var file = new ByteArrayContent(Png);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "logo", "logo.png");
            var saved = await client.PostAsync("/Admin/Branding", form);
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        }

        var anonymous = factory.CreateClient();
        var home = await anonymous.GetStringAsync("/");
        Assert.Contains("کلینیک نمونه دکتر رضایی</title>", home);
        Assert.Contains("به کلینیک نمونه خوش آمدید", home);
        Assert.Contains("--olive-600:#2f5f9e;", home);
        Assert.Matches("src=\"/media/brand/[^\"]+\\.png\"", home);
        Assert.DoesNotContain("/img/logo.png", home);
        var english = await anonymous.GetStringAsync("/en");
        Assert.Contains("Dr. Rezaei Sample Clinic</title>", english);
        Assert.Contains("Sample Clinic", english);

        var reset = await PostFormAsync(client, "/Admin/Branding?handler=ResetColors", "/Admin/Branding", new());
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        home = await anonymous.GetStringAsync("/");
        Assert.DoesNotContain("--olive-600:", home);
        Assert.Contains("کلینیک نمونه دکتر رضایی</title>", home);
    }

    private async Task<ApplicationUser> OwnerAsync()
    {
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = (await users.FindByEmailAsync(OwnerWebFactory.OwnerEmail))!;
        Assert.True(await users.IsInRoleAsync(owner, Roles.Owner));
        return owner;
    }

    private async Task<ApplicationUser> CreateUserAsync(string role)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = $"{role} test" };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRoleAsync(user, role);
        return user;
    }

    private async Task AddRoleAsync(ApplicationUser user, string role)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await users.AddToRoleAsync((await users.FindByIdAsync(user.Id))!, role);
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await PostFormAsync(client, "/Account/Login", "/Account/Login", new() { ["Input.Email"] = email, ["Input.Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, string formPage, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(formPage);
        fields["__RequestVerificationToken"] = TokenPattern().Match(html).Groups[1].Value;
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
