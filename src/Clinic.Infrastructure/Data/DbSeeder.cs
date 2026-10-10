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

        foreach (var role in Roles.All.Append(Roles.Owner))
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

        await SeedAdminAsync(db, userManager, config, logger);
        await SeedOwnerAsync(userManager, config, logger);
    }

    private static async Task SeedAdminAsync(ClinicDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
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

    /// <summary>
    /// Makes the account named by <c>Owner:Email</c> the one and only site owner (and an admin). The owner role cannot be
    /// given from any page; whoever controls the server's configuration decides it. If the account does not exist yet and
    /// <c>Owner:Password</c> is set, it is created. Without <c>Owner:Email</c> nothing changes.
    /// </summary>
    private static async Task SeedOwnerAsync(UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
        var email = config["Owner:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var owner = await userManager.FindByEmailAsync(email);
        if (owner is null)
        {
            var password = config["Owner:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Owner:Email {Email} has no account yet; set Owner:Password to create it.", email);
                return;
            }
            owner = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = config["Owner:Name"] ?? "Owner",
            };
            var created = await userManager.CreateAsync(owner, password);
            if (!created.Succeeded)
            {
                logger.LogError("Could not create the site owner: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }
        }

        foreach (var other in await userManager.GetUsersInRoleAsync(Roles.Owner))
        {
            if (other.Id != owner.Id)
            {
                await userManager.RemoveFromRoleAsync(other, Roles.Owner);
                await userManager.UpdateSecurityStampAsync(other);
            }
        }

        var changed = false;
        foreach (var role in new[] { Roles.Owner, Roles.Admin })
        {
            if (!await userManager.IsInRoleAsync(owner, role))
            {
                await userManager.AddToRoleAsync(owner, role);
                changed = true;
            }
        }
        if (!owner.IsActive)
        {
            owner.IsActive = true;
            await userManager.UpdateAsync(owner);
            changed = true;
        }
        if (changed)
        {
            await userManager.UpdateSecurityStampAsync(owner);
        }
    }
}
