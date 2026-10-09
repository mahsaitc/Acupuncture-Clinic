using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

public partial class ClinicalTests
{
    [Fact]
    public async Task Doctor_sees_only_their_own_patients_appointments_and_messages()
    {
        var mine = await CreateUserAsync(Roles.Patient, "بیمار خودم");
        var theirs = await CreateUserAsync(Roles.Patient, "بیمار همکار");
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor), mine);
        var colleague = await AssignAsync(await CreateUserAsync(Roles.Doctor), theirs);
        var day = DateTime.UtcNow.Date.AddDays(3);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            int Profile(ApplicationUser u) => db.Doctors.Single(d => d.UserId == u.Id).Id;
            var service = await db.Services.FirstAsync();
            db.Appointments.AddRange(
                new Appointment { DoctorProfileId = Profile(doctor), PatientUserId = mine.Id, ClinicServiceId = service.Id, StartUtc = day.AddHours(6), EndUtc = day.AddHours(7) },
                new Appointment { DoctorProfileId = Profile(colleague), PatientUserId = theirs.Id, ClinicServiceId = service.Id, StartUtc = day.AddHours(8), EndUtc = day.AddHours(9) });
            db.ContactMessages.AddRange(
                new ContactMessage { Name = "a", Phone = "09120000000", Subject = "پیام برای من", Body = "...", DoctorProfileId = Profile(doctor) },
                new ContactMessage { Name = "b", Phone = "09120000000", Subject = "پیام برای همکار", Body = "...", DoctorProfileId = Profile(colleague) },
                new ContactMessage { Name = "c", Phone = "09120000000", Subject = "پیام عمومی کلینیک", Body = "..." });
            await db.SaveChangesAsync();
        }
        var client = await LoginAsync(doctor);

        var patients = await client.GetStringAsync("/Admin/Patients");
        Assert.Contains("بیمار خودم", patients);
        Assert.DoesNotContain("بیمار همکار", patients);

        var calendar = await client.GetStringAsync($"/Admin/Appointments?View=week&Date={day:yyyy-MM-dd}");
        Assert.Contains("بیمار خودم", calendar);
        Assert.DoesNotContain("بیمار همکار", calendar);

        var messages = await client.GetStringAsync("/Admin/Messages");
        Assert.Contains("پیام برای من", messages);
        Assert.DoesNotContain("پیام برای همکار", messages);
        Assert.DoesNotContain("پیام عمومی کلینیک", messages);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Admin/Records?patientId={mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Admin/Records?patientId={theirs.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Admin/Records/Files?patientId={theirs.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Admin/Patients/Details?id={theirs.Id}")).StatusCode);

        // Working hours are set by the admin only; the doctor sees theirs on the dashboard.
        var schedule = await client.GetAsync("/Admin/Schedule");
        Assert.Equal(HttpStatusCode.Redirect, schedule.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", schedule.Headers.Location!.PathAndQuery);
        Assert.Contains("ساعات کاری من", await client.GetStringAsync("/Admin"));
    }

    [Fact]
    public async Task Admin_registers_a_doctor_and_sets_their_hours()
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Admin));
        var email = $"doc-{Guid.NewGuid():N}@test";
        var response = await PostPairsAsync(client, "/Admin/UserCreate",
        [
            new("Input.FullName", "دکتر نمونه"), new("Input.Email", email), new("Input.PhoneNumber", "09" + Random.Shared.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture)),
            new("Input.Password", Password), new("Input.ConfirmPassword", Password),
            new("SelectedRoles", Roles.Doctor), new("SelectedRoles", Roles.Receptionist),
            new("MedicalCouncilNumber", "۱۲۳۴۵"), new("SpecialtyFa", "طب سوزنی"),
        ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Admin/Schedule", response.Headers.Location!.OriginalString);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.Equal([Roles.Doctor, Roles.Receptionist], (await users.GetRolesAsync(user)).Order());
        var profile = await db.Doctors.SingleAsync(d => d.UserId == user.Id);
        Assert.True(profile.IsApproved);
        Assert.Equal("12345", profile.MedicalCouncilNumber);

        var add = await PostFormAsync(client, $"/Admin/Schedule?handler=Add&DoctorId={profile.Id}",
            new() { ["day"] = nameof(DayOfWeek.Saturday), ["start"] = "09:00", ["end"] = "12:00" });
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        Assert.Single(db.WorkingHours.Where(w => w.DoctorProfileId == profile.Id));
    }

    [Fact]
    public async Task Patient_picks_a_doctor_and_the_note_reaches_their_inbox()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor), patient);
        int doctorId, serviceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var profile = await db.Doctors.Include(d => d.WorkingHours).SingleAsync(d => d.UserId == doctor.Id);
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                profile.WorkingHours.Add(new WorkingHour { DayOfWeek = day, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0) });
            }
            await db.SaveChangesAsync();
            doctorId = profile.Id;
            serviceId = (await db.Services.FirstAsync()).Id;
        }
        var client = await LoginAsync(patient);
        var url = $"/Booking?ServiceId={serviceId}&DoctorId={doctorId}&Date={DateTime.UtcNow.AddDays(2):yyyy-MM-dd}";
        var page = await client.GetStringAsync(url);
        Assert.Contains(doctor.FullName, page);
        var ticks = Regex.Match(page, "name=\"startTicks\" value=\"(\\d+)\"").Groups[1].Value;

        var response = await PostFormAsync(client, url, new() { ["startTicks"] = ticks, ["note"] = "لطفاً درباره رژیم هم صحبت کنیم" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var message = await check.ServiceProvider.GetRequiredService<ClinicDbContext>().ContactMessages.SingleAsync(m => m.UserId == patient.Id);
        Assert.Equal(doctorId, message.DoctorProfileId);
        Assert.NotNull(message.AppointmentId);
        Assert.Contains("رژیم", await (await LoginAsync(doctor)).GetStringAsync("/Admin/Messages"));
    }
}
