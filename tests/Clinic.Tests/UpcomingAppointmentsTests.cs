using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
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

    [Fact]
    public async Task Staff_book_for_a_front_desk_patient_by_mobile_and_self_bookings_are_not_linked()
    {
        var mobile = "0915" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);
        var patient = await CreateUserAsync(Roles.Patient, "بیمار با موبایل");
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر نوبت‌دهنده"), patient);
        int doctorId, serviceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            (await db.Users.SingleAsync(u => u.Id == patient.Id)).PhoneNumber = mobile;
            var profile = await db.Doctors.Include(d => d.WorkingHours).SingleAsync(d => d.UserId == doctor.Id);
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                profile.WorkingHours.Add(new WorkingHour { DayOfWeek = day, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0) });
            }
            await db.SaveChangesAsync();
            doctorId = profile.Id;
            serviceId = (await db.Services.FirstAsync()).Id;
        }
        var client = await LoginAsync(doctor);
        var url = $"/Booking?ServiceId={serviceId}&DoctorId={doctorId}&Date={DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var ticks = Regex.Matches(await client.GetStringAsync(url), "name=\"startTicks\" value=\"(\\d+)\"").Select(m => m.Groups[1].Value).ToList();

        // Persian digits are accepted.
        var persian = string.Concat(mobile.Select(c => (char)('۰' + (c - '0'))));
        Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(client, url, new() { ["startTicks"] = ticks[0], ["patientEmail"] = persian })).StatusCode);
        // An unknown number is refused rather than booked for the doctor.
        Assert.Contains("بیماری با این موبایل یا ایمیل پیدا نشد", await (await PostFormAsync(client, url, new() { ["startTicks"] = ticks[1], ["patientEmail"] = "09000000000" })).Content.ReadAsStringAsync());
        // Left empty, the doctor books for themselves: a staff account with no patient page.
        Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(client, url, new() { ["startTicks"] = ticks[2] })).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            Assert.True(await db.Appointments.AnyAsync(a => a.PatientUserId == patient.Id && a.DoctorProfileId == doctorId));
        }

        var admin = await LoginAsync(await CreateUserAsync(Roles.Admin));
        var page = await admin.GetStringAsync($"/Admin/Doctors/Patients?id={doctorId}");
        Assert.Contains($"/Admin/Patients/Details?id={patient.Id}", page);
        Assert.DoesNotContain($"/Admin/Patients/Details?id={doctor.Id}", page);
        Assert.Contains("دکتر نوبت‌دهنده", page);
    }
}
