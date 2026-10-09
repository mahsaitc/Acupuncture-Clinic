using System.Net;
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
    public async Task Doctor_sends_a_post_and_only_the_admin_publishes_edits_or_deletes_it()
    {
        var doctor = await LoginAsync(await CreateUserAsync(Roles.Doctor));
        var title = $"مطلب پزشک {Guid.NewGuid():N}";
        var sent = await PostFormAsync(doctor, "/Admin/Posts/Edit?kind=Blog", new()
        {
            ["Input.TitleFa"] = title, ["Input.BodyFa"] = "متن مطلب", ["Input.Slug"] = $"doctor-post-{Guid.NewGuid():N}", ["Input.IsPublished"] = "true",
        });
        Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var post = await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().Posts.SingleAsync(p => p.TitleFa == title);
            Assert.False(post.IsPublished);
            id = post.Id;
        }

        var edit = await doctor.GetAsync($"/Admin/Posts/Edit?id={id}&kind=Blog");
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Contains("در انتظار تأیید مدیر", await doctor.GetStringAsync("/Admin/Posts?kind=Blog"));
        foreach (var handler in new[] { "Publish&publish=true", "Delete" })
        {
            var refused = await PostFormAsync(doctor, $"/Admin/Posts?handler={handler}&id={id}&Kind=Blog", []);
            Assert.StartsWith("/Account/AccessDenied", refused.Headers.Location!.PathAndQuery);
        }
        using (var scope = factory.Services.CreateScope())
        {
            Assert.False((await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().Posts.SingleAsync(p => p.Id == id)).IsPublished);
        }

        var admin = await LoginAsync(await CreateUserAsync(Roles.Admin));
        Assert.Contains("در انتظار بررسی", await admin.GetStringAsync("/Admin/Posts?kind=Blog"));
        Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(admin, $"/Admin/Posts?handler=Publish&id={id}&publish=true&Kind=Blog", [])).StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.True((await check.ServiceProvider.GetRequiredService<ClinicDbContext>().Posts.SingleAsync(p => p.Id == id)).IsPublished);
    }

    [Fact]
    public async Task Admin_case_summary_shows_each_doctor_with_their_sessions_and_points()
    {
        var patient = await CreateUserAsync(Roles.Patient, "بیمار خلاصه");
        var first = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر اول"), patient);
        var second = await CreateUserAsync(Roles.Doctor, "دکتر دوم");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            TreatmentSession Session(string doctorId, SessionType type, params string[] codes) => new()
            {
                PatientUserId = patient.Id, DoctorUserId = doctorId, DateUtc = DateTime.UtcNow, Type = type,
                Points = codes.Select(c => new SessionPoint { Code = c, Label = c, Side = PointSide.Both }).ToList(),
            };
            db.TreatmentSessions.AddRange(
                Session(first.Id, SessionType.Acupuncture, "ST36", "LI4"),
                Session(first.Id, SessionType.Acupuncture, "ST36"),
                Session(second.Id, SessionType.CatgutEmbedding, "CV12"));
            await db.SaveChangesAsync();
        }

        var admin = await LoginAsync(await ClinicAdminAsync());
        var summary = await admin.GetStringAsync($"/Admin/Patients/Summary?id={patient.Id}");
        Assert.Contains("دکتر اول", summary);
        Assert.Contains("دکتر دوم", summary);
        Assert.Contains("ST36 <span class=\"text-muted\">× ۲</span>", summary);
        Assert.Contains("CV12", summary);

        Assert.Contains($"/Admin/Records/Print?patientId={patient.Id}", summary);

        // A doctor sees and prints only their own part.
        var own = await (await LoginAsync(first)).GetStringAsync($"/Admin/Patients/Summary?id={patient.Id}");
        Assert.Contains("دکتر اول", own);
        Assert.DoesNotContain("دکتر دوم", own);
        Assert.DoesNotContain("CV12", own);
        Assert.DoesNotContain("/Admin/Records/Print", own);

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var response = await reception.GetAsync($"/Admin/Patients/Summary?id={patient.Id}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Each_doctor_sees_only_their_own_sessions_and_the_admin_print_names_the_doctor_of_each()
    {
        var patient = await CreateUserAsync(Roles.Patient, "بیمار جلسات");
        var first = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر جلسه اول"), patient);
        var second = await CreateUserAsync(Roles.Doctor, "دکتر جلسه دوم");
        int othersSession;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var mine = new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = first.Id, DateUtc = DateTime.UtcNow.AddDays(-2), Complaint = "شکایت جلسه اول" };
            var others = new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = second.Id, DateUtc = DateTime.UtcNow.AddDays(-1), Complaint = "شکایت جلسه دوم" };
            db.TreatmentSessions.AddRange(mine, others);
            await db.SaveChangesAsync();
            othersSession = others.Id;
        }

        var doctor = await LoginAsync(first);
        foreach (var url in new[] { $"/Admin/Records?patientId={patient.Id}", $"/Admin/Records/Print?patientId={patient.Id}" })
        {
            var page = await doctor.GetStringAsync(url);
            Assert.Contains("دکتر جلسه اول", page);
            Assert.DoesNotContain("دکتر جلسه دوم", page);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await doctor.GetAsync($"/Admin/Records/Session?patientId={patient.Id}&id={othersSession}")).StatusCode);
        var token = await TokenAsync(doctor, $"/Admin/Records/Session?patientId={patient.Id}");
        var delete = await doctor.PostAsync($"/Admin/Records/Session?handler=Delete&patientId={patient.Id}&id={othersSession}",
            new FormUrlEncodedContent([new("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var admin = await LoginAsync(await ClinicAdminAsync());
        var print = await admin.GetStringAsync($"/Admin/Records/Print?patientId={patient.Id}");
        Assert.Contains("شکایت جلسه اول", print);
        Assert.Contains("شکایت جلسه دوم", print);
        Assert.Contains("<td class=\"doctor\">دکتر جلسه اول</td>", print);
        Assert.Contains("<td class=\"doctor\">دکتر جلسه دوم</td>", print);
    }

    [Fact]
    public async Task Admin_and_reception_see_the_treating_doctor_in_the_patient_list()
    {
        var patient = await CreateUserAsync(Roles.Patient, $"بیمار فهرست {Guid.NewGuid():N}");
        await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر فهرست"), patient);
        foreach (var role in new[] { Roles.Admin, Roles.Receptionist })
        {
            var list = await (await LoginAsync(await CreateUserAsync(role))).GetStringAsync($"/Admin/Patients?Q={Uri.EscapeDataString(patient.FullName)}");
            Assert.Contains("پزشک معالج", list);
            Assert.Contains("دکتر فهرست", list);
        }
    }

    [Fact]
    public async Task Receptionist_sees_the_doctor_of_each_appointment_but_not_messages_for_doctors()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر نوبت"), patient);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var profile = await db.Doctors.SingleAsync(d => d.UserId == doctor.Id);
            var service = await db.Services.FirstAsync();
            var start = DateTime.UtcNow.Date.AddDays(-5).AddHours(7);
            db.Appointments.Add(new Appointment { DoctorProfileId = profile.Id, PatientUserId = patient.Id, ClinicServiceId = service.Id, StartUtc = start, EndUtc = start.AddMinutes(30), PatientNote = "یادداشت خصوصی برای پزشک" });
            db.ContactMessages.Add(new ContactMessage { Name = "x", Phone = "09120000000", Subject = "پیام محرمانه برای پزشک", Body = "...", UserId = patient.Id, DoctorProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var details = await reception.GetStringAsync($"/Admin/Patients/Details?id={patient.Id}");
        Assert.Contains("دکتر نوبت", details);
        Assert.DoesNotContain("یادداشت خصوصی برای پزشک", details);
        Assert.DoesNotContain("پیام محرمانه برای پزشک", details);
        Assert.DoesNotContain("پیام محرمانه برای پزشک", await reception.GetStringAsync("/Admin/Messages"));
    }

    /// <summary>An admin who is also a doctor, like the clinic's own admin account.</summary>
    private async Task<ApplicationUser> ClinicAdminAsync()
    {
        var admin = await CreateUserAsync(Roles.Admin);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id))!, Roles.Doctor);
        return admin;
    }
}
