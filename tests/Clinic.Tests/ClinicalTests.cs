using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Clinic.Application.Acupuncture;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

public partial class ClinicalTests(ClinicWebFactory factory) : IClassFixture<ClinicWebFactory>
{
    private const string Password = "Passw0rd!";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Fact]
    public async Task Receptionist_cannot_open_medical_records()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var client = await LoginAsync(await CreateUserAsync(Roles.Receptionist));

        var record = await client.GetAsync($"/Admin/Records?patientId={patient.Id}");
        var details = await client.GetStringAsync($"/Admin/Patients/Details?id={patient.Id}");

        Assert.Equal(HttpStatusCode.Redirect, record.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", record.Headers.Location!.PathAndQuery);
        Assert.DoesNotContain("/Admin/Records", details);
    }

    [Fact]
    public async Task Receptionist_registers_a_patient_with_personal_details()
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var mobile = $"0912{Random.Shared.Next(1000000, 9999999)}";

        var response = await PostFormAsync(client, "/Admin/Patients/Create", new()
        {
            ["Input.FullName"] = "مریم کریمی",
            ["Input.Mobile"] = JalaliDateDigits(mobile),
            ["Input.NationalCode"] = "۰۰۱۲۳۴۵۶۷۸",
            ["Input.BirthDate"] = "۱۳۶۵/۰۴/۱۲",
            ["Input.Gender"] = nameof(Gender.Female),
            ["Input.City"] = "تهران",
            ["Input.Address"] = "خیابان ولیعصر",
            ["Input.Referral"] = nameof(ReferralChannel.Instagram),
            ["Input.ReferralSource"] = "صفحه کلینیک",
            ["Input.Insurance"] = nameof(BasicInsurance.SocialSecurity),
            ["Input.HasSupplementaryInsurance"] = "true",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var user = await db.Users.SingleAsync(u => u.PhoneNumber == mobile);
        Assert.Null(user.Email);
        Assert.Null(user.PasswordHash);
        var profile = await db.PatientProfiles.SingleAsync(p => p.UserId == user.Id);
        Assert.Equal("0012345678", profile.NationalCode);
        Assert.Equal(new DateOnly(1986, 7, 3), profile.BirthDate);
        var raw = await db.Database.SqlQuery<string>($"SELECT NationalCode AS Value FROM PatientProfiles WHERE Id = {profile.Id}").SingleAsync();
        Assert.DoesNotContain("0012345678", raw);

        var details = await client.GetStringAsync($"/Admin/Patients/Details?id={user.Id}");
        Assert.Contains("اینستاگرام - صفحه کلینیک", details);
        Assert.Equal(ReferralChannel.Instagram, profile.Referral);
        Assert.Equal(BasicInsurance.SocialSecurity, profile.Insurance);
        Assert.True(profile.HasSupplementaryInsurance);
        Assert.Contains("تأمین اجتماعی", details);
        Assert.Contains("بیمه تکمیلی", details);
        Assert.DoesNotContain("/Admin/Records", details);

        var again = await PostFormAsync(client, "/Admin/Patients/Create", new() { ["Input.FullName"] = "تکراری", ["Input.Mobile"] = mobile });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(1, await db.Users.CountAsync(u => u.PhoneNumber == mobile));
    }

    [Fact]
    public async Task Registering_a_patient_without_mobile_shows_the_form_again()
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var name = $"بدون موبایل {Guid.NewGuid():N}";

        var response = await PostFormAsync(client, "/Admin/Patients/Create", new()
        {
            ["Input.FullName"] = name,
            ["Input.Mobile"] = "",
            ["Input.LandlinePhone"] = "",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("وارد کردن شماره موبایل الزامی است.", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.FullName == name));
    }

    [Theory]
    [InlineData("Input.NationalCode", "۰۰۱۲۳۴۵۶۷", "کد ملی باید دقیقاً ۱۰ رقم باشد.")]
    [InlineData("Input.LandlinePhone", "0713234642", "با کد شهر")]
    [InlineData("Input.PostalCode", "71345", "کد پستی باید دقیقاً ۱۰ رقم باشد.")]
    [InlineData("Input.Mobile", "091212345678", "شماره موبایل را ۱۱ رقمی وارد کنید")]
    public async Task Patient_form_rejects_wrong_digit_counts(string field, string value, string message)
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var form = new Dictionary<string, string>
        {
            ["Input.FullName"] = "آزمون",
            ["Input.Mobile"] = $"0912{Random.Shared.Next(1000000, 9999999)}",
        };
        form[field] = value;

        var response = await PostFormAsync(client, "/Admin/Patients/Create", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Patient_form_marks_required_fields_and_offers_a_calendar()
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var html = await client.GetStringAsync("/Admin/Patients/Create");

        Assert.Matches("<label class=\"form-label required\" for=\"Input_FullName\"", html);
        Assert.Matches("<label class=\"form-label required\" for=\"Input_Mobile\"", html);
        Assert.DoesNotMatch("<label class=\"form-label required\" for=\"Input_City\"", html);
        Assert.Matches("<input(?=[^>]*data-date)(?=[^>]*name=\"Input.BirthDate\")", html);
        Assert.Contains("/js/forms", html);
        Assert.Contains("value=\"Website\"", html);
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    public async Task Clinic_calendar_has_day_week_and_month_views(string view)
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var html = await client.GetStringAsync($"/Admin/Appointments?View={view}&Date=2026-10-06");

        switch (view)
        {
            case "week":
                Assert.Contains("week-grid", html);
                Assert.Contains("۱۱ مهر ۱۴۰۵", html); // the week starts on Saturday 1405/07/11
                break;
            case "month":
                Assert.Contains("month-grid", html);
                Assert.Contains("مهر ۱۴۰۵", html);
                break;
        }
    }

    [Fact]
    public async Task Medical_files_over_200_kb_are_refused()
    {
        var client = await LoginAsync(await CreateUserAsync(Roles.Patient));
        var big = new byte[201 * 1024];
        "%PDF-1.7"u8.CopyTo(big);

        var response = await UploadAsync(client, "/Files", "scan.pdf", "application/pdf", big, "Upload.Title", "Scan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("۲۰۰ کیلوبایت", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Doctor_saves_record_with_persian_digits_and_diagnosis_is_encrypted()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var client = await LoginAsync(await AssignAsync(await CreateUserAsync(Roles.Doctor), patient));
        var url = $"/Admin/Records/Edit?patientId={patient.Id}";

        // Tick boxes send one value per box, so this form has repeated keys.
        var response = await PostPairsAsync(client, url,
        [
            new("Input.WeightKg", "۸۲٫۵"),
            new("Input.HeightCm", "165"),
            new("Input.HipCm", "۱۰۴"),
            new("Input.Diagnosis", "چاقی"),
            new("Input.HasDrugAllergy", "true"),
            new("Input.DrugAllergies", "پنی‌سیلین"),
            new("Input.Conditions", "Diabetes"),
            new("Input.Conditions", "Thyroid"),
            new("Input.Goals", "WeightLoss"),
            new("Input.Sleep", "Poor"),
            new("Input.Methods", "Embedding"),
            new("Input.Methods", "Needling"),
            new("Input.LastMenstrualPeriod", "۱۴۰۵/۰۶/۲۸"),
            new("Input.DoctorNotes", "پیگیری قند خون"),
        ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var record = await db.MedicalRecords.SingleAsync(r => r.PatientUserId == patient.Id);
        Assert.Equal(82.5, record.WeightKg);
        Assert.Equal(104, record.HipCm);
        Assert.Equal("چاقی", record.Diagnosis);
        Assert.Equal(MedicalCondition.Diabetes | MedicalCondition.Thyroid, record.Conditions);
        Assert.Equal(TreatmentGoal.WeightLoss, record.Goals);
        Assert.Equal(SleepQuality.Poor, record.Sleep);
        Assert.Equal(TreatmentMethod.Needling | TreatmentMethod.Embedding, record.Methods);
        Assert.True(record.HasDrugAllergy);
        Assert.Equal(new DateOnly(2026, 9, 19), record.LastMenstrualPeriod);

        var raw = await db.Database.SqlQuery<string>($"SELECT Diagnosis AS Value FROM MedicalRecords WHERE Id = {record.Id}").SingleAsync();
        Assert.DoesNotContain("چاقی", raw);
        Assert.Contains(db.AuditEntries, a => a.PatientUserId == patient.Id && a.Action == AuditAction.UpdateRecord);

        var page = await client.GetStringAsync($"/Admin/Records?patientId={patient.Id}");
        Assert.Contains("پنی‌سیلین", page);
        Assert.Contains("دیابت", page);

        // The edit form ticks what was saved, and the printable form shows every section.
        var edit = await client.GetStringAsync(url);
        Assert.Matches("value=\"Thyroid\"[^>]*checked", edit);
        var print = await client.GetStringAsync($"/Admin/Records/Print?patientId={patient.Id}");
        Assert.Contains("☑ دیابت", print);
        Assert.Contains("پیگیری قند خون", print);
        Assert.Contains("/img/logo", print);
    }

    private static string JalaliDateDigits(string latin) => Clinic.Application.Common.JalaliDate.ToPersianDigits(latin);

    [Fact]
    public async Task Doctor_records_a_session_with_points()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var client = await LoginAsync(await AssignAsync(await CreateUserAsync(Roles.Doctor), patient));
        var json = """
            [{"code":"ST36","label":"ST36 Zusanli","side":"Right"},
             {"code":"EAR-1","label":"Shenmen ear","view":"ear","side":"Left","x":500,"y":40,"note":"seed"}]
            """;

        var response = await PostFormAsync(client, $"/Admin/Records/Session?patientId={patient.Id}", new()
        {
            ["Input.Date"] = "1405/07/14",
            ["Input.Time"] = "16:30",
            ["Input.Type"] = nameof(SessionType.CatgutEmbedding),
            ["Input.WeightKg"] = "80.2",
            ["Input.PointsJson"] = json,
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var session = await db.TreatmentSessions.Include(s => s.Points).SingleAsync(s => s.PatientUserId == patient.Id);
        Assert.Equal(SessionType.CatgutEmbedding, session.Type);
        Assert.Equal(new DateTime(2026, 10, 6, 13, 0, 0), session.DateUtc);
        Assert.Collection(session.Points.OrderBy(p => p.Id),
            p => { Assert.Equal("ST36", p.Code); Assert.Equal(PointSide.Right, p.Side); },
            p => { Assert.Null(p.Code); Assert.Equal("ear", p.View); Assert.Equal(200, p.X); Assert.Equal(PointSide.Left, p.Side); Assert.Equal("seed", p.Note); });

        var page = await client.GetStringAsync($"/Admin/Records?patientId={patient.Id}");
        Assert.Contains("ST36", page);
        Assert.Contains("weight-chart", page);
    }

    [Fact]
    public async Task Patient_sees_only_their_shared_files()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var other = await CreateUserAsync(Roles.Patient);
        var doctor = await CreateUserAsync(Roles.Doctor);
        int shared, hidden, foreign;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            MedicalFile File(string owner, string title, bool visible)
            {
                var name = $"{Guid.NewGuid():N}.png";
                Directory.CreateDirectory(factory.PrivateFilesRoot);
                System.IO.File.WriteAllBytes(Path.Combine(factory.PrivateFilesRoot, name), Png);
                var f = new MedicalFile
                {
                    PatientUserId = owner, Title = title, StoredName = name, ContentType = "image/png", SizeBytes = Png.Length,
                    UploadedByUserId = doctor.Id, UploadedUtc = DateTime.UtcNow, VisibleToPatient = visible,
                };
                db.MedicalFiles.Add(f);
                return f;
            }
            var a = File(patient.Id, "shared-mri", true);
            var b = File(patient.Id, "hidden-photo", false);
            var c = File(other.Id, "someone-else", true);
            await db.SaveChangesAsync();
            (shared, hidden, foreign) = (a.Id, b.Id, c.Id);
        }

        var client = await LoginAsync(patient);
        var page = await client.GetStringAsync("/Files");
        var open = await client.GetAsync($"/Files?handler=Open&id={shared}");

        Assert.Contains("shared-mri", page);
        Assert.DoesNotContain("hidden-photo", page);
        Assert.DoesNotContain("someone-else", page);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        Assert.Equal("image/png", open.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", open.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Files?handler=Open&id={hidden}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Files?handler=Open&id={foreign}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/Admin/Records/File?id={shared}")).StatusCode);
    }

    [Fact]
    public async Task Patient_upload_is_checked_and_reaches_the_record()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var client = await LoginAsync(patient);

        var fake = await UploadAsync(client, "/Files", "lab.pdf", "application/pdf", "not a pdf"u8.ToArray(), "Upload.Title", "Fake");
        var real = await UploadAsync(client, "/Files", "lab.pdf", "application/pdf", "%PDF-1.7 test"u8.ToArray(), "Upload.Title", "CBC");

        Assert.Equal(HttpStatusCode.OK, fake.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, real.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var file = await db.MedicalFiles.SingleAsync(f => f.PatientUserId == patient.Id);
        Assert.Equal("CBC", file.Title);
        Assert.Equal(FileCategory.Lab, file.Category);
        Assert.True(File.Exists(Path.Combine(factory.PrivateFilesRoot, file.StoredName)));
        Assert.Contains(db.AuditEntries, a => a.PatientUserId == patient.Id && a.Action == AuditAction.UploadFile);
    }

    [Fact]
    public async Task Doctor_opening_a_file_is_audited()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var client = await LoginAsync(await AssignAsync(await CreateUserAsync(Roles.Doctor), patient));
        var upload = await UploadAsync(client, $"/Admin/Records/Files?handler=Upload&patientId={patient.Id}", "x.png", "image/png", Png, "Upload.Title", "X-ray");
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);

        int id;
        using (var scope = factory.Services.CreateScope())
        {
            id = (await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().MedicalFiles.SingleAsync(f => f.PatientUserId == patient.Id)).Id;
        }
        var response = await client.GetAsync($"/Admin/Records/File?id={id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.Contains(check.ServiceProvider.GetRequiredService<ClinicDbContext>().AuditEntries,
            a => a.PatientUserId == patient.Id && a.Action == AuditAction.ViewFile && a.EntityId == id.ToString());
    }

    private async Task<ApplicationUser> CreateUserAsync(string role, string? name = null)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = name ?? $"{role} test" };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRoleAsync(user, role);
        return user;
    }

    /// <summary>Makes the user a bookable doctor and the patient one of theirs; a doctor opens only their own patients.</summary>
    private async Task<ApplicationUser> AssignAsync(ApplicationUser doctor, ApplicationUser patient)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var profile = await db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctor.Id);
        if (profile is null)
        {
            profile = new DoctorProfile { UserId = doctor.Id, MedicalCouncilNumber = "1234", IsApproved = true };
            db.Doctors.Add(profile);
            await db.SaveChangesAsync();
        }
        var patientProfile = await db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == patient.Id);
        if (patientProfile is null)
        {
            db.PatientProfiles.Add(new PatientProfile { UserId = patient.Id, DoctorProfileId = profile.Id });
        }
        else
        {
            patientProfile.DoctorProfileId = profile.Id;
        }
        await db.SaveChangesAsync();
        return doctor;
    }

    private async Task<HttpClient> LoginAsync(ApplicationUser user)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await PostFormAsync(client, "/Account/Login", new() { ["Input.Email"] = user.Email!, ["Input.Password"] = Password });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<string> TokenAsync(HttpClient client, string url)
    {
        var page = await client.GetAsync(url);
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.IsSuccessStatusCode, $"GET {url} returned {(int)page.StatusCode}: {html[..Math.Min(html.Length, 3000)]}");
        return TokenPattern().Match(html).Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostPairsAsync(HttpClient client, string url, List<KeyValuePair<string, string>> fields)
    {
        fields.Add(new("__RequestVerificationToken", await TokenAsync(client, url)));
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await TokenAsync(client, url);
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, string fileName, string contentType, byte[] bytes, string titleField, string title)
    {
        var token = await TokenAsync(client, Regex.Replace(url, "handler=[^&]+&?", ""));
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(title), titleField },
            { file, "file", fileName },
        };
        return await client.PostAsync(url, form);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
