using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Clinic.Domain;
using Clinic.Infrastructure;
using Clinic.Infrastructure.Data;
using Clinic.Web;
using Clinic.Web.Localization;
using Clinic.Web.Media;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddClinicInfrastructure(builder.Configuration);
// Emit Persian text as-is instead of &#x...; entities.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddScoped<DisplayFormat>();
builder.Services.AddSingleton<MediaStore>();
builder.Services.AddScoped<SiteContentProvider>();
builder.Services.AddSingleton<Clinic.Web.Content.MarkdownRenderer>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Clinic.Web.Content.ContactThrottle>();
// Allow the hero video upload through the form reader; the page itself enforces the real limit.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = MediaStore.MaxVideoBytes + MediaStore.MaxImageBytes + 1024 * 1024);
builder.Services.AddScoped<IdentityErrorDescriber, LocalizedIdentityErrorDescriber>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddRazorPages(options =>
    {
        // The whole management panel needs a staff role; pages narrow it further with [Authorize(Policy = ...)].
        options.Conventions.AuthorizeFolder("/Admin", Policies.Staff);
        options.Conventions.AuthorizeFolder("/Appointments");
        options.Conventions.AuthorizeFolder("/Booking");
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin))
    .AddPolicy(Policies.Doctor, p => p.RequireRole(Roles.Doctor))
    .AddPolicy(Policies.Content, p => p.RequireRole(Roles.Admin, Roles.Doctor))
    .AddPolicy(Policies.Staff, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Receptionist));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// A deactivated user or a role change takes effect within five minutes, not at cookie expiry.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = CulturePath.Supported.Select(c => new CultureInfo(c)).ToList();
    options.DefaultRequestCulture = new RequestCulture(CulturePath.Persian);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = [new CulturePath.Provider()];
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().Database.MigrateAsync();
    }
    await DbSeeder.SeedAsync(app.Services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCulturePathPrefix();
app.UseRequestLocalization();

// Uploaded public media (hero video and poster). Medical files are never stored here.
var media = app.Services.GetRequiredService<MediaStore>();
Directory.CreateDirectory(media.Root);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(media.Root),
    RequestPath = MediaStore.RequestPath,
    ServeUnknownFileTypes = false,
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
