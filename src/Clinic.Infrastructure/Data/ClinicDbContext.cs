using Clinic.Domain.Entities;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Clinic.Infrastructure.Data;

public class ClinicDbContext(DbContextOptions<ClinicDbContext> options, IDataProtectionProvider protection)
    : IdentityDbContext<ApplicationUser>(options)
{
    /// <summary>Changing this makes existing encrypted values unreadable.</summary>
    public const string ProtectionPurpose = "Clinic.MedicalRecord.v1";

    public DbSet<DoctorProfile> Doctors => Set<DoctorProfile>();
    public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();
    public DbSet<ClinicService> Services => Set<ClinicService>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<SiteContent> SiteContent => Set<SiteContent>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<TreatmentSession> TreatmentSessions => Set<TreatmentSession>();
    public DbSet<SessionPoint> SessionPoints => Set<SessionPoint>();
    public DbSet<MedicalFile> MedicalFiles => Set<MedicalFile>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

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
            e.Property(c => c.Email).HasMaxLength(200);
            e.Property(c => c.OpeningHoursFa).HasMaxLength(300);
            e.Property(c => c.OpeningHoursEn).HasMaxLength(300);
            e.Property(c => c.MapEmbedUrl).HasMaxLength(2000);
            e.Property(c => c.MapLinkUrl).HasMaxLength(500);
        });

        builder.Entity<ContactMessage>(e =>
        {
            e.Property(m => m.Name).HasMaxLength(200).IsRequired();
            e.Property(m => m.Phone).HasMaxLength(30).IsRequired();
            e.Property(m => m.Email).HasMaxLength(200);
            e.Property(m => m.Subject).HasMaxLength(200).IsRequired();
            e.Property(m => m.Body).HasMaxLength(4000).IsRequired();
            e.HasIndex(m => new { m.IsArchived, m.CreatedUtc });
        });

        builder.Entity<Post>(e =>
        {
            e.Property(p => p.Slug).HasMaxLength(150).IsRequired();
            e.HasIndex(p => new { p.Kind, p.Slug }).IsUnique();
            e.HasIndex(p => new { p.Kind, p.IsPublished, p.PublishedUtc });
            e.Property(p => p.TitleFa).HasMaxLength(250).IsRequired();
            e.Property(p => p.TitleEn).HasMaxLength(250);
            e.Property(p => p.SummaryFa).HasMaxLength(600);
            e.Property(p => p.SummaryEn).HasMaxLength(600);
            e.Property(p => p.BodyFa).IsRequired();
            e.Property(p => p.CoverImagePath).HasMaxLength(300);
            e.Property(p => p.References).HasMaxLength(4000);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // The model is built once per process, so the converter keeps the protector of the first context.
        // The provider is an application singleton, so every context uses the same keys anyway.
        var protector = protection.CreateProtector(ProtectionPurpose);
        var encrypted = new ValueConverter<string?, string?>(
            v => v == null ? null : protector.Protect(v),
            v => v == null ? null : protector.Unprotect(v));

        builder.Entity<PatientProfile>(e =>
        {
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.NationalCode).HasConversion(encrypted).HasMaxLength(500);
            e.Property(p => p.FatherName).HasMaxLength(100);
            e.Property(p => p.Occupation).HasMaxLength(100);
            e.Property(p => p.Education).HasMaxLength(100);
            e.Property(p => p.LandlinePhone).HasMaxLength(30);
            e.Property(p => p.City).HasMaxLength(100);
            e.Property(p => p.Address).HasMaxLength(400);
            e.Property(p => p.PostalCode).HasMaxLength(20);
            e.Property(p => p.ReferralSource).HasMaxLength(200);
            e.Property(p => p.InsuranceProvider).HasMaxLength(100);
            e.Property(p => p.EmergencyContactName).HasMaxLength(200);
            e.Property(p => p.EmergencyContactPhone).HasMaxLength(30);
            e.Property(p => p.Notes).HasMaxLength(2000);
        });

        builder.Entity<MedicalRecord>(e =>
        {
            e.HasIndex(r => r.PatientUserId).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.PatientUserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(r => r.Diagnosis).HasConversion(encrypted).HasMaxLength(8000);
            e.Property(r => r.Icd10).HasMaxLength(50);
            e.Property(r => r.ProblemDuration).HasMaxLength(200);
            e.Property(r => r.TongueColor).HasMaxLength(200);
            e.Property(r => r.TongueCoating).HasMaxLength(200);
            e.Property(r => r.SessionInterval).HasMaxLength(200);
            e.Property(r => r.OtherAreas).HasMaxLength(500);
            e.Property(r => r.TcmPattern).HasMaxLength(1000);
            foreach (var name in new[]
            {
                nameof(MedicalRecord.ChiefComplaint), nameof(MedicalRecord.GoalsNotes), nameof(MedicalRecord.OtherConditions),
                nameof(MedicalRecord.Medications), nameof(MedicalRecord.Surgeries), nameof(MedicalRecord.DrugAllergies),
                nameof(MedicalRecord.OtherAllergies), nameof(MedicalRecord.ObservedChanges), nameof(MedicalRecord.ShortTermGoals),
                nameof(MedicalRecord.LongTermGoals), nameof(MedicalRecord.AfterCareAdvice),
            })
            {
                e.Property(name).HasMaxLength(2000);
            }
            e.Property(r => r.DoctorNotes).HasMaxLength(8000);
        });

        builder.Entity<TreatmentSession>(e =>
        {
            e.HasIndex(s => new { s.PatientUserId, s.DateUtc });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.PatientUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.DoctorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(s => s.Points).WithOne().HasForeignKey(p => p.TreatmentSessionId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.Complaint).HasMaxLength(2000);
            e.Property(s => s.Reactions).HasMaxLength(2000);
            e.Property(s => s.Notes).HasMaxLength(4000);
            e.Property(s => s.NextPlan).HasMaxLength(2000);
        });

        builder.Entity<SessionPoint>(e =>
        {
            e.Property(p => p.Code).HasMaxLength(20);
            e.Property(p => p.Label).HasMaxLength(100).IsRequired();
            e.Property(p => p.View).HasMaxLength(20);
            e.Property(p => p.Note).HasMaxLength(200);
        });

        builder.Entity<MedicalFile>(e =>
        {
            e.HasIndex(f => new { f.PatientUserId, f.UploadedUtc });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(f => f.PatientUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(f => f.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(f => f.Title).HasMaxLength(200).IsRequired();
            e.Property(f => f.Note).HasMaxLength(1000);
            e.Property(f => f.StoredName).HasMaxLength(100).IsRequired();
            e.HasIndex(f => f.StoredName).IsUnique();
            e.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        });

        builder.Entity<AuditEntry>(e =>
        {
            e.HasIndex(a => a.TimestampUtc);
            e.HasIndex(a => new { a.PatientUserId, a.TimestampUtc });
            e.Property(a => a.UserId).HasMaxLength(450).IsRequired();
            e.Property(a => a.PatientUserId).HasMaxLength(450).IsRequired();
            e.Property(a => a.EntityId).HasMaxLength(50);
            e.Property(a => a.Ip).HasMaxLength(64);
        });
    }
}
