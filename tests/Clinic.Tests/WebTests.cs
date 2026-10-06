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
    [InlineData("/Staff/Appointments", "/Account/Login")]
    [InlineData("/Doctor/Schedule", "/Account/Login")]
    [InlineData("/Admin/Site", "/Account/Login")]
    public async Task Protected_pages_redirect_anonymous_users_to_login(string path, string login)
    {
        var response = await Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(login, response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }
}
