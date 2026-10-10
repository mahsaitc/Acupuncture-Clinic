using System.Net;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

public partial class ClinicalTests
{
    [Fact]
    public async Task Reception_sees_a_doctors_upcoming_appointments_and_the_admin_edits_their_file()
    {
        var patient = await CreateUserAsync(Roles.Patient, $"بیمار نوبت آینده {Guid.NewGuid():N}");
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر نوبت آینده"), patient);
        int doctorId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var profile = await db.Doctors.SingleAsync(d => d.UserId == doctor.Id);
            doctorId = profile.Id;
            var start = DateTime.UtcNow.Date.AddDays(3).AddHours(6);
            db.Appointments.Add(new Appointment { DoctorProfileId = profile.Id, PatientUserId = patient.Id, ClinicServiceId = (await db.Services.FirstAsync()).Id, StartUtc = start, EndUtc = start.AddMinutes(30) });
            await db.SaveChangesAsync();
        }

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var page = await reception.GetStringAsync($"/Admin/Doctors/Patients?id={doctorId}");
        Assert.Contains("نوبت‌های پیش رو", page);
        Assert.Contains(patient.FullName, page);
        Assert.DoesNotContain("پرونده ثبت‌نام پزشک", page);
        Assert.StartsWith("/Account/AccessDenied", (await reception.GetAsync($"/Admin/Doctors/Edit?id={doctorId}")).Headers.Location!.PathAndQuery);

        var admin = await LoginAsync(await CreateUserAsync(Roles.Admin));
        Assert.Contains("پرونده ثبت‌نام پزشک", await admin.GetStringAsync($"/Admin/Doctors/Patients?id={doctorId}"));
        var council = NewCouncilNumber();
        var saved = await PostFormAsync(admin, $"/Admin/Doctors/Edit?id={doctorId}", new()
        {
            ["Input.FullName"] = "دکتر ویرایش‌شده", ["Input.Email"] = doctor.Email!, ["Input.PhoneNumber"] = "09125556677",
            ["Input.NationalCode"] = NewNationalCode(), ["Input.MedicalCouncilNumber"] = council, ["Input.SpecialtyFa"] = "طب سوزنی",
        });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var token = await TokenAsync(admin, $"/Admin/Doctors/Edit?id={doctorId}");
        using (var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(nameof(DoctorDocumentKind.MedicalLicense)), "kind" },
        })
        {
            var file = new ByteArrayContent(Png);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "licence.png");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/Admin/Doctors/Edit?id={doctorId}&handler=Upload", form)).StatusCode);
        }

        int documentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var profile = await db.Doctors.Include(d => d.Documents).SingleAsync(d => d.Id == doctorId);
            Assert.Equal(council, profile.MedicalCouncilNumber);
            Assert.Equal("دکتر ویرایش‌شده", (await db.Users.SingleAsync(u => u.Id == doctor.Id)).FullName);
            documentId = Assert.Single(profile.Documents).Id;
        }

        // Another doctor cannot take the same Medical Council number.
        var other = await AssignAsync(await CreateUserAsync(Roles.Doctor), patient);
        int otherId;
        using (var scope = factory.Services.CreateScope())
        {
            otherId = (await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().Doctors.SingleAsync(d => d.UserId == other.Id)).Id;
        }
        var clash = await PostFormAsync(admin, $"/Admin/Doctors/Edit?id={otherId}", new()
        {
            ["Input.FullName"] = "دکتر دیگر", ["Input.Email"] = other.Email!, ["Input.NationalCode"] = NewNationalCode(), ["Input.MedicalCouncilNumber"] = council,
        });
        Assert.Contains("این شماره نظام پزشکی قبلاً برای پزشک دیگری ثبت شده است", await clash.Content.ReadAsStringAsync());

        await PostFormAsync(admin, $"/Admin/Doctors/Edit?id={doctorId}&handler=DeleteDocument&documentId={documentId}", []);
        using var check = factory.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<ClinicDbContext>().DoctorDocuments.AnyAsync(d => d.Id == documentId));
    }

    [Fact]
    public async Task Home_page_shows_results_marked_as_sample_until_the_admin_enters_real_ones()
    {
        var home = factory.CreateClient();
        var page = await home.GetStringAsync("/");
        Assert.Contains("id=\"results\"", page);
        Assert.Contains("داده‌های نمونه", page);
        Assert.Contains("کمردرد", page);

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        Assert.StartsWith("/Account/AccessDenied", (await reception.GetAsync("/Admin/Results")).Headers.Location!.PathAndQuery);

        var admin = await LoginAsync(await CreateUserAsync(Roles.Admin));
        try
        {
            var saved = await PostPairsAsync(admin, "/Admin/Results",
            [
                new("Results.Show", "true"), new("Results.IsSample", "false"),
                new("Results.TitleFa", "نتایج واقعی"), new("Results.NoteFa", "۱۵۰ بیمار، ۱۴۰۴"),
                new("Results.Tiles[0].Icon", "people"), new("Results.Tiles[0].Value", "۱۵۰"), new("Results.Tiles[0].LabelFa", "بیمار"),
                new("Results.Tiles[1].Icon", "star"), new("Results.Tiles[1].Value", ""), new("Results.Tiles[1].LabelFa", ""),
                new("Results.Charts[0].TitleFa", "کاهش وزن"), new("Results.Charts[0].Kind", "BeforeAfter"), new("Results.Charts[0].ShowTable", "true"),
                new("Results.Charts[0].Rows[0].LabelFa", "دوره ۱۰ جلسه‌ای"), new("Results.Charts[0].Rows[0].Value", "۸۵٫۵"), new("Results.Charts[0].Rows[0].After", "79"),
                new("Results.Charts[0].Rows[1].LabelFa", ""), new("Results.Charts[0].Rows[1].Value", ""),
                new("Results.Charts[1].TitleFa", ""), new("Results.Charts[1].Rows[0].LabelFa", ""),
            ]);
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

            page = await home.GetStringAsync("/");
            Assert.Contains("نتایج واقعی", page);
            Assert.DoesNotContain("داده‌های نمونه", page);
            Assert.DoesNotContain("کمردرد", page);
            Assert.Contains("۸۵.۵", page);
            Assert.Contains("۱۵۰ بیمار، ۱۴۰۴", page);
            using var scope = factory.Services.CreateScope();
            var results = (await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().SiteContent.SingleAsync()).Results();
            Assert.Single(results.Tiles);
            Assert.Single(Assert.Single(results.Charts).Rows);
        }
        finally
        {
            await PostFormAsync(admin, "/Admin/Results?handler=Sample", []);
        }
    }
}
