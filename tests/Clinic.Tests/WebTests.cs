using System.Net;
using Microsoft.AspNetCore.Hosting;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

public sealed class ClinicWebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clinic-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Clinic", $"Data Source={_dbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}

public class WebTests(ClinicWebFactory factory) : IClassFixture<ClinicWebFactory>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Root_is_persian_and_right_to_left()
    {
        var html = await Client().GetStringAsync("/");

        Assert.Contains("lang=\"fa\" dir=\"rtl\"", html);
        Assert.Contains("کلینیک طب سوزنی", html);
        Assert.Contains("bootstrap.rtl.min", html);
        Assert.Contains("طب سوزنی", html);
    }

    [Fact]
    public async Task En_prefix_is_english_and_left_to_right()
    {
        var html = await Client().GetStringAsync("/en");

        Assert.Contains("lang=\"en\" dir=\"ltr\"", html);
        Assert.Contains("Acupuncture Clinic", html);
        Assert.Contains("href=\"/en/Account/Login\"", html);
        Assert.Contains("Catgut embedding", html);
    }

    [Fact]
    public async Task Hero_plays_the_uploaded_video()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var content = await db.SiteContent.FindAsync(SiteContent.SingletonId);
            content!.HeroVideoPath = "/media/hero/test.mp4";
            content.HeroPosterPath = "/media/hero/test.jpg";
            content.Phone = "02112345678";
            await db.SaveChangesAsync();
        }

        var html = await Client().GetStringAsync("/");

        Assert.Contains("<video class=\"hero-media\" autoplay muted loop playsinline", html);
        Assert.Contains("src=\"/media/hero/test.mp4\" type=\"video/mp4\"", html);
        Assert.Contains("poster=\"/media/hero/test.jpg\"", html);
        Assert.Contains("href=\"tel:02112345678\"", html);
    }

    [Fact]
    public async Task Contact_form_saves_the_message()
    {
        var client = factory.CreateClient();
        var form = await client.GetStringAsync("/Contact");
        var token = System.Text.RegularExpressions.Regex.Match(form, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/Contact", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Name"] = "سارا احمدی",
            ["Input.Phone"] = "09121234567",
            ["Input.Subject"] = "سؤال درباره کاشت نخ",
            ["Input.Body"] = "چند جلسه برای کاشت نخ لازم است؟",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("پیام شما ارسال شد", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        Assert.Contains(db.ContactMessages, m => m.Subject == "سؤال درباره کاشت نخ" && m.ReadUtc == null);
    }

    [Fact]
    public async Task Contact_form_drops_messages_from_bots()
    {
        var client = factory.CreateClient();
        var form = await client.GetStringAsync("/Contact");
        var token = System.Text.RegularExpressions.Regex.Match(form, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        await client.PostAsync("/Contact", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Website"] = "http://spam.example",
            ["Input.Name"] = "Bot",
            ["Input.Phone"] = "09120000000",
            ["Input.Subject"] = "spam-subject",
            ["Input.Body"] = "buy cheap things now",
        }));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        Assert.DoesNotContain(db.ContactMessages, m => m.Subject == "spam-subject");
    }

    [Fact]
    public async Task Contact_page_shows_the_map_and_social_links()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var content = await db.SiteContent.FindAsync(SiteContent.SingletonId);
            content!.MapEmbedUrl = "https://www.google.com/maps/embed?pb=!1m18";
            content.InstagramUrl = "https://instagram.com/clinic";
            await db.SaveChangesAsync();
        }

        var html = await Client().GetStringAsync("/Contact");

        Assert.Contains("<iframe src=\"https://www.google.com/maps/embed?pb=!1m18\"", html);
        Assert.Contains("href=\"https://instagram.com/clinic\"", html);
    }

    [Fact]
    public async Task Blog_shows_only_published_posts_and_english_only_when_translated()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var author = new Clinic.Infrastructure.Identity.ApplicationUser { UserName = "author@x", Email = "author@x", FullName = "دکتر" };
            db.Users.Add(author);
            db.Posts.AddRange(
                new Post { Kind = PostKind.Blog, Slug = "published-fa", TitleFa = "مطلب منتشرشده", BodyFa = "متن **مهم**", AuthorUserId = author.Id, IsPublished = true, PublishedUtc = DateTime.UtcNow },
                new Post { Kind = PostKind.Blog, Slug = "draft", TitleFa = "پیش‌نویس محرمانه", BodyFa = "x", AuthorUserId = author.Id },
                new Post { Kind = PostKind.Blog, Slug = "both", TitleFa = "دوزبانه", BodyFa = "x", TitleEn = "Bilingual post", BodyEn = "<script>alert(1)</script> body", AuthorUserId = author.Id, IsPublished = true, PublishedUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var fa = await Client().GetStringAsync("/Blog");
        var en = await Client().GetStringAsync("/en/Blog");
        var post = await Client().GetStringAsync("/Blog/published-fa");
        var enPost = await Client().GetStringAsync("/en/Blog/both");

        Assert.Contains("مطلب منتشرشده", fa);
        Assert.DoesNotContain("پیش‌نویس محرمانه", fa);
        Assert.Contains("Bilingual post", en);
        Assert.DoesNotContain("مطلب منتشرشده", en);
        Assert.Contains("<strong>مهم</strong>", post);
        Assert.DoesNotContain("<script>alert(1)</script>", enPost);
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync("/Blog/draft")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync("/en/Blog/published-fa")).StatusCode);
    }

    [Fact]
    public async Task Fa_prefix_is_an_alias_of_the_root()
    {
        var html = await Client().GetStringAsync("/fa/Account/Login");

        Assert.Contains("dir=\"rtl\"", html);
    }

    [Fact]
    public async Task Language_switch_points_to_the_same_page()
    {
        var fa = await Client().GetStringAsync("/Account/Register");
        var en = await Client().GetStringAsync("/en/Account/Register");

        Assert.Contains("href=\"/en/Account/Register\"", fa);
        Assert.Contains("href=\"/Account/Register\"", en);
    }

    [Theory]
    [InlineData("/Booking", "/Account/Login")]
    [InlineData("/en/Booking", "/en/Account/Login")]
    [InlineData("/Admin/Users", "/Account/Login")]
    [InlineData("/Admin", "/Account/Login")]
    [InlineData("/Admin/Appointments", "/Account/Login")]
    [InlineData("/Admin/Schedule", "/Account/Login")]
    [InlineData("/Admin/Site", "/Account/Login")]
    [InlineData("/Admin/Patients", "/Account/Login")]
    [InlineData("/Admin/Messages", "/Account/Login")]
    [InlineData("/Admin/Posts?kind=Blog", "/Account/Login")]
    [InlineData("/en/Admin/Posts/Edit?kind=Article", "/en/Account/Login")]
    public async Task Protected_pages_redirect_anonymous_users_to_login(string path, string login)
    {
        var response = await Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(login, response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }
}
