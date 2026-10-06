using Clinic.Domain.Entities;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Data;

public class ClinicDbContext(DbContextOptions<ClinicDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<DoctorProfile> Doctors => Set<DoctorProfile>();
    public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();
    public DbSet<ClinicService> Services => Set<ClinicService>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<SiteContent> SiteContent => Set<SiteContent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.PreferredLanguage).HasMaxLength(5);
        });

        builder.Entity<DoctorProfile>(e =>
        {
            e.HasIndex(d => d.UserId).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(d => d.MedicalCouncilNumber).HasMaxLength(20).IsRequired();
            e.HasMany(d => d.WorkingHours).WithOne().HasForeignKey(w => w.DoctorProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClinicService>(e =>
        {
            e.Property(s => s.NameFa).HasMaxLength(100).IsRequired();
            e.Property(s => s.NameEn).HasMaxLength(100).IsRequired();
        });

        builder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.Doctor).WithMany().HasForeignKey(a => a.DoctorProfileId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Service).WithMany().HasForeignKey(a => a.ClinicServiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.PatientUserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.PatientNote).HasMaxLength(500);
            e.HasIndex(a => new { a.DoctorProfileId, a.StartUtc });
            e.HasIndex(a => a.PatientUserId);
        });

        builder.Entity<SiteContent>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.HeroTitleFa).HasMaxLength(200);
            e.Property(c => c.HeroTitleEn).HasMaxLength(200);
            e.Property(c => c.HeroSubtitleFa).HasMaxLength(500);
            e.Property(c => c.HeroSubtitleEn).HasMaxLength(500);
            e.Property(c => c.HeroVideoPath).HasMaxLength(300);
            e.Property(c => c.HeroPosterPath).HasMaxLength(300);
            e.Property(c => c.Phone).HasMaxLength(30);
            e.Property(c => c.AddressFa).HasMaxLength(300);
            e.Property(c => c.AddressEn).HasMaxLength(300);
            e.Property(c => c.InstagramUrl).HasMaxLength(300);
            e.Property(c => c.WhatsAppUrl).HasMaxLength(300);
            e.Property(c => c.TelegramUrl).HasMaxLength(300);
            e.Property(c => c.YouTubeUrl).HasMaxLength(300);
        });
    }
}
