using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clinic.Infrastructure.Data;

public static class DbSeeder
{
    /// <summary>
    /// Creates roles, default services and (if configured) the first admin account.
    /// The admin password comes from configuration (user secrets or an environment variable), never from source.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ClinicDbContext>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder));

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (!await db.Services.AnyAsync())
        {
            db.Services.AddRange(
                new ClinicService { NameFa = "طب سوزنی", NameEn = "Acupuncture", DurationMinutes = 45 },
                new ClinicService { NameFa = "کاشت نخ (کتگوت)", NameEn = "Catgut embedding", DurationMinutes = 30 },
                new ClinicService { NameFa = "ویزیت عمومی", NameEn = "General consultation", DurationMinutes = 15 });
            await db.SaveChangesAsync();
        }

        if (!await db.SiteContent.AnyAsync())
        {
            db.SiteContent.Add(new SiteContent());
            await db.SaveChangesAsync();
        }

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }
        if (await userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = config["Seed:AdminName"] ?? "Admin",
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            logger.LogError("Could not create the seed admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }
        await userManager.AddToRolesAsync(admin, [Roles.Admin, Roles.Doctor]);
        db.Doctors.Add(new DoctorProfile
        {
            UserId = admin.Id,
            MedicalCouncilNumber = config["Seed:AdminMedicalCouncilNumber"] ?? "0",
            IsApproved = true,
        });
        await db.SaveChangesAsync();
    }
}
