using System.Net;
using System.Text.RegularExpressions;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

/// <summary>A site with the request limits and the bot check switched on, as in production (no waiting time, so tests stay fast).</summary>
public sealed class SecuredWebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clinic-secure-{Guid.NewGuid():N}.db");
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"clinic-secure-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Clinic", $"Data Source={_dbPath}");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(_dataDir, "keys"));
        builder.UseSetting("PrivateFiles:Root", Path.Combine(_dataDir, "private"));
        builder.UseSetting("Media:Root", Path.Combine(_dataDir, "media"));
        builder.UseSetting("Security:BotCheckMinSeconds", "0");
        builder.UseSetting("Site:BaseUrl", "https://clinic.example");
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

public partial class SeoSecurityTests(SecuredWebFactory factory) : IClassFixture<SecuredWebFactory>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Pages_send_security_headers_and_scripts_carry_the_nonce()
    {
        var response = await Client().GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        var nonce = Regex.Match(csp, "'nonce-([^']+)'").Groups[1].Value;
        Assert.NotEmpty(nonce);
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'self'", csp);
        Assert.DoesNotContain("unsafe-inline' 'nonce", csp);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("SAMEORIGIN", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.False(response.Headers.Contains("Server"));
        foreach (Match script in Regex.Matches(html, "<script[^>]*>"))
        {
            Assert.Contains($"nonce=\"{nonce}\"", script.Value);
        }
    }

    [Fact]
    public void Views_have_no_inline_event_handlers_the_policy_would_block()
    {
        var pages = Path.Combine(WebRoot(), "Pages");
        var offenders = Directory.EnumerateFiles(pages, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\son[a-z]+\s*=\s*""", RegexOptions.IgnoreCase))
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/en/Account/Login")]
    public async Task Private_pages_are_not_indexed_or_cached(string url)
    {
        var response = await Client().GetAsync(url);

        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Robots_file_hides_private_pages_and_names_the_sitemap()
    {
        var text = await Client().GetStringAsync("/robots.txt");

        Assert.Contains("Disallow: /Admin", text);
        Assert.Contains("Disallow: /en/Account", text);
        Assert.Contains("Sitemap: https://clinic.example/sitemap.xml", text);
    }

    [Fact]
    public async Task Sitemap_lists_public_pages_and_published_posts_in_their_languages()
    {
        var both = await AddPostAsync(english: true);
        var persianOnly = await AddPostAsync(english: false);

        var xml = await Client().GetStringAsync("/sitemap.xml");

        Assert.Contains("encoding=\"utf-8\"", xml);
        Assert.Contains("<loc>https://clinic.example/</loc>", xml);
        Assert.Contains("<loc>https://clinic.example/en/Contact</loc>", xml);
        Assert.Contains($"<loc>https://clinic.example/Blog/{both.Slug}</loc>", xml);
        Assert.Contains($"<loc>https://clinic.example/en/Blog/{both.Slug}</loc>", xml);
        Assert.Contains($"<loc>https://clinic.example/Blog/{persianOnly.Slug}</loc>", xml);
        Assert.DoesNotContain($"/en/Blog/{persianOnly.Slug}", xml);
        Assert.DoesNotContain("/Admin", xml);
    }

    [Fact]
    public async Task Home_page_has_canonical_language_links_previews_and_clinic_data()
    {
        var html = await Client().GetStringAsync("/");

        Assert.Contains("<link rel=\"canonical\" href=\"https://clinic.example/\" />", html);
        Assert.Contains("hreflang=\"en\" href=\"https://clinic.example/en\"", html);
        Assert.Contains("hreflang=\"x-default\" href=\"https://clinic.example/\"", html);
        Assert.Contains("<meta name=\"description\"", html);
        Assert.Contains("<meta property=\"og:image\" content=\"https://clinic.example/", html);
        Assert.Contains("<meta property=\"og:locale\" content=\"fa_IR\" />", html);
        Assert.Contains("\"@type\":\"MedicalClinic\"", html);
        Assert.Contains("\"telephone\":\"07132346425\"", html);
        Assert.DoesNotContain("noindex", html);
    }

    [Fact]
    public async Task Post_page_describes_the_article_for_search_engines()
    {
        var post = await AddPostAsync(english: false);

        var html = await Client().GetStringAsync($"/Blog/{post.Slug}");

        Assert.Contains($"<link rel=\"canonical\" href=\"https://clinic.example/Blog/{post.Slug}\" />", html);
        Assert.Contains("<meta property=\"og:type\" content=\"article\" />", html);
        Assert.Contains("\"@type\":\"BlogPosting\"", html);
        Assert.Contains("\"@type\":\"BreadcrumbList\"", html);
        Assert.Contains("<meta name=\"description\" content=\"خلاصه آزمایشی\" />", html);
        // No English version: no English alternate pointing at a missing page.
        Assert.DoesNotContain("rel=\"alternate\" hreflang=\"en\"", html);
    }

    [Fact]
    public async Task Sign_up_without_the_bot_check_fields_is_refused()
    {
        var client = Client();
        var email = $"bot-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(RegisterFields(await FormAsync(client, "/Account/Register"), email, withStamp: false)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("فرم خیلی سریع ارسال شد", await response.Content.ReadAsStringAsync());
        Assert.False(await UserExistsAsync(email));
    }

    [Fact]
    public async Task Sign_up_with_the_hidden_field_filled_is_refused()
    {
        var client = Client();
        var email = $"trap-{Guid.NewGuid():N}@example.com";
        var fields = RegisterFields(await FormAsync(client, "/Account/Register"), email, withStamp: true);
        fields[Clinic.Web.Security.BotCheck.TrapField] = "http://spam.example";

        await client.PostAsync("/Account/Register", new FormUrlEncodedContent(fields));

        Assert.False(await UserExistsAsync(email));
    }

    [Fact]
    public async Task Sign_up_from_the_real_form_goes_through()
    {
        var client = Client();
        var email = $"person-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(RegisterFields(await FormAsync(client, "/Account/Register"), email, withStamp: true)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await UserExistsAsync(email));
    }

    [Fact]
    public async Task Many_login_attempts_from_one_address_are_slowed_down()
    {
        var client = Client();
        var html = await FormAsync(client, "/en/Account/Login");
        var token = TokenPattern().Match(html).Groups[1].Value;
        HttpResponseMessage? last = null;

        for (var i = 0; i <= Clinic.Web.Security.RequestLimits.Rules.Single(r => r.Path == "/Account/Login").Permits; i++)
        {
            last = await client.PostAsync("/en/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.Email"] = "nobody@example.com",
                ["Input.Password"] = "wrong-password",
            }));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Contains("Too many attempts", await last.Content.ReadAsStringAsync());
        // Reading pages is never limited.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/en/Account/Login")).StatusCode);
    }

    private static Dictionary<string, string> RegisterFields(string html, string email, bool withStamp)
    {
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = TokenPattern().Match(html).Groups[1].Value,
            ["Input.FullName"] = "بیمار آزمایشی",
            ["Input.Email"] = email,
            ["Input.PhoneNumber"] = "09" + Random.Shared.Next(100000000, 999999999),
            ["Input.Password"] = "Patient1234",
            ["Input.ConfirmPassword"] = "Patient1234",
        };
        if (withStamp)
        {
            fields[Clinic.Web.Security.BotCheck.StampField] = StampPattern().Match(html).Groups[1].Value;
        }
        return fields;
    }

    private static async Task<string> FormAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().Users.AnyAsync(u => u.Email == email);
    }

    private async Task<Post> AddPostAsync(bool english)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var author = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync();
        if (author is null)
        {
            var user = new ApplicationUser { UserName = $"author-{Guid.NewGuid():N}@example.com", Email = "author@example.com", FullName = "نویسنده" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            author = user.Id;
        }
        var post = new Post
        {
            Kind = PostKind.Blog,
            Slug = $"seo-test-{Guid.NewGuid():N}",
            TitleFa = "مطلب آزمایشی",
            SummaryFa = "خلاصه آزمایشی",
            BodyFa = "متن آزمایشی",
            TitleEn = english ? "Test post" : null,
            BodyEn = english ? "Test body" : null,
            AuthorUserId = author,
            IsPublished = true,
            PublishedUtc = DateTime.UtcNow,
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Clinic.Web")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir!.FullName, "src", "Clinic.Web");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("name=\"__FormStamp\" value=\"([^\"]+)\"")]
    private static partial Regex StampPattern();
}
