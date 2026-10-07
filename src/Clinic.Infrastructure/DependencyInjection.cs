using Clinic.Application.Common;
using Clinic.Application.Scheduling;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Scheduling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClinicInfrastructure(this IServiceCollection services, IConfiguration config, string contentRoot)
    {
        // These keys encrypt login cookies and the encrypted medical record fields.
        // Losing them makes those fields unreadable, so the folder must be backed up with the database.
        var keys = Path.GetFullPath(config["DataProtection:KeysPath"] ?? Path.Combine(contentRoot, "keys"));
        services.AddDataProtection()
            .SetApplicationName("AcupunctureClinic")
            .PersistKeysToFileSystem(new DirectoryInfo(keys));

        var connection = config.GetConnectionString("Clinic")
                         ?? throw new InvalidOperationException("Connection string 'Clinic' is missing.");

        // SQLite for now. When hosting is chosen, switch the provider here and regenerate the migrations.
        services.AddDbContext<ClinicDbContext>(options => options.UseSqlite(connection));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Patients registered at the front desk may have no email; uniqueness is checked where accounts are created.
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ClinicDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new ClinicTime(config["Clinic:TimeZone"] ?? "Asia/Tehran"));
        services.AddScoped<IBookingService, BookingService>();

        return services;
    }
}
