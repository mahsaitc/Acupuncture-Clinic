using Clinic.Application.Common;
using Clinic.Application.Scheduling;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClinicInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Clinic")
                         ?? throw new InvalidOperationException("Connection string 'Clinic' is missing.");

        // SQLite for now. When hosting is chosen, switch the provider here and regenerate the migrations.
        services.AddDbContext<ClinicDbContext>(options => options.UseSqlite(connection));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
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
