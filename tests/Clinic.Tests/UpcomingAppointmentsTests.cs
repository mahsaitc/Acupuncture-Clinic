using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

public partial class ClinicalTests
{
    [Fact]
    public async Task Next_seven_days_lists_each_doctors_own_appointments_and_all_for_reception()
    {
        var mine = await CreateUserAsync(Roles.Patient, "بیمار هفته من");
        var theirs = await CreateUserAsync(Roles.Patient, "بیمار هفته همکار");
        var cancelled = await CreateUserAsync(Roles.Patient, "بیمار لغو شده");
        var later = await CreateUserAsync(Roles.Patient, "بیمار ماه بعد");
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر هفته"), mine);
        var colleague = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر همکار هفته"), theirs);
        var day = DateTime.UtcNow.Date.AddDays(2);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            int Profile(ApplicationUser u) => db.Doctors.Single(d => d.UserId == u.Id).Id;
            var service = await db.Services.FirstAsync();
            Appointment At(ApplicationUser doc, ApplicationUser patient, DateTime start) =>
                new() { DoctorProfileId = Profile(doc), PatientUserId = patient.Id, ClinicServiceId = service.Id, StartUtc = start, EndUtc = start.AddHours(1) };
            var gone = At(doctor, cancelled, day.AddHours(9));
            gone.Status = AppointmentStatus.Cancelled;
            db.Appointments.AddRange(
                At(doctor, mine, day.AddHours(6)),
                At(colleague, theirs, day.AddDays(1).AddHours(7)),
                gone,
                At(doctor, later, day.AddDays(12).AddHours(6)));
            await db.SaveChangesAsync();
        }

        // The dashboard figure opens the list.
        var doctorClient = await LoginAsync(doctor);
        Assert.Contains("View=upcoming", await doctorClient.GetStringAsync("/Admin"));

        var own = await doctorClient.GetStringAsync("/Admin/Appointments?View=upcoming");
        Assert.Contains("بیمار هفته من", own);
        Assert.DoesNotContain("بیمار هفته همکار", own);
        Assert.DoesNotContain("بیمار لغو شده", own);
        Assert.DoesNotContain("بیمار ماه بعد", own);

        var all = await (await LoginAsync(await CreateUserAsync(Roles.Receptionist, "منشی هفته"))).GetStringAsync("/Admin/Appointments?View=upcoming");
        Assert.Contains("بیمار هفته من", all);
        Assert.Contains("بیمار هفته همکار", all);
        Assert.Contains("دکتر همکار هفته", all);
    }

    [Theory]
    [InlineData(Roles.Doctor, "پزشک")]
    [InlineData(Roles.Receptionist, "منشی")]
    public async Task Panel_shows_the_signed_in_users_name_and_role(string role, string roleName)
    {
        var name = $"کاربر {Guid.NewGuid():N}"[..14];
        var html = await (await LoginAsync(await CreateUserAsync(role, name))).GetStringAsync("/Admin");

        var header = html[html.IndexOf("admin-user", StringComparison.Ordinal)..];
        Assert.Contains(name, header);
        Assert.Contains(roleName, header[..400]);
    }
}
