using System.Globalization;
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
    public async Task A_medical_council_number_belongs_to_one_doctor()
    {
        var council = NewCouncilNumber();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await DoctorSignUpAsync(client, $"c1-{Guid.NewGuid():N}@test", NewNationalCode(), council)).StatusCode);

        var email = $"c2-{Guid.NewGuid():N}@test";
        var again = await DoctorSignUpAsync(factory.CreateClient(new() { AllowAutoRedirect = false }), email, NewNationalCode(), council);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("این شماره نظام پزشکی قبلاً برای پزشک دیگری ثبت شده است", await again.Content.ReadAsStringAsync());

        var admin = await LoginAsync(await CreateUserAsync(Roles.Admin));
        var created = await PostPairsAsync(admin, "/Admin/UserCreate",
        [
            new("Input.FullName", "دکتر تکراری"), new("Input.Email", email), new("Input.PhoneNumber", "09" + Random.Shared.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture)),
            new("Input.Password", Password), new("Input.ConfirmPassword", Password),
            new("SelectedRoles", Roles.Doctor), new("MedicalCouncilNumber", council), new("NationalCode", NewNationalCode()),
        ]);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Contains("این شماره نظام پزشکی قبلاً برای پزشک دیگری ثبت شده است", await created.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task Admin_and_reception_list_doctors_and_open_their_patients()
    {
        var patient = await CreateUserAsync(Roles.Patient, $"بیمار پزشک {Guid.NewGuid():N}");
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, $"دکتر لیست {Guid.NewGuid():N}"), patient);
        int doctorId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var profile = await db.Doctors.SingleAsync(d => d.UserId == doctor.Id);
            profile.NationalCode = "4440001112";
            doctorId = profile.Id;
            db.TreatmentSessions.Add(new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        var admin = await LoginAsync(await ClinicAdminAsync());
        var list = await admin.GetStringAsync("/Admin/Doctors");
        Assert.Contains(doctor.FullName, list);
        Assert.Contains("۴۴۴۰۰۰۱۱۱۲", list);
        Assert.Contains(patient.FullName, list);
        var patients = await admin.GetStringAsync($"/Admin/Doctors/Patients?id={doctorId}");
        Assert.Contains(patient.FullName, patients);
        Assert.Contains($"/Admin/Records?patientId={patient.Id}", patients);

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        list = await reception.GetStringAsync("/Admin/Doctors");
        Assert.Contains(doctor.FullName, list);
        Assert.DoesNotContain("۴۴۴۰۰۰۱۱۱۲", list);
        patients = await reception.GetStringAsync($"/Admin/Doctors/Patients?id={doctorId}");
        Assert.Contains(patient.FullName, patients);
        Assert.DoesNotContain("/Admin/Records", patients);

        var own = await (await LoginAsync(doctor)).GetAsync("/Admin/Doctors");
        Assert.StartsWith("/Account/AccessDenied", own.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Doctor_replies_edits_and_deletes_and_the_patient_answers_back()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر پاسخ"), patient);
        int messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var profile = await db.Doctors.SingleAsync(d => d.UserId == doctor.Id);
            var message = new ContactMessage { Name = patient.FullName, Phone = "09120000000", Subject = "سؤال درباره درمان", Body = "سلام", UserId = patient.Id, DoctorProfileId = profile.Id };
            db.ContactMessages.Add(message);
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        var staff = await LoginAsync(doctor);
        Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(staff, $"/Admin/Messages?handler=Reply&id={messageId}", new() { ["body"] = "پاسخ اول پزشک" })).StatusCode);
        int replyId;
        using (var scope = factory.Services.CreateScope())
        {
            replyId = (await scope.ServiceProvider.GetRequiredService<ClinicDbContext>().MessageReplies.SingleAsync(r => r.ContactMessageId == messageId)).Id;
        }
        await PostFormAsync(staff, $"/Admin/Messages?handler=EditReply&replyId={replyId}", new() { ["body"] = "پاسخ ویرایش‌شده پزشک" });

        var client = await LoginAsync(patient);
        var mine = await client.GetStringAsync("/Messages");
        Assert.Contains("پاسخ ویرایش‌شده پزشک", mine);
        Assert.DoesNotContain("پاسخ اول پزشک", mine);
        Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(client, $"/Messages?handler=Reply&id={messageId}", new() { ["body"] = "ممنون، سؤال دیگری دارم" })).StatusCode);

        var inbox = await staff.GetStringAsync("/Admin/Messages");
        Assert.Contains("ممنون، سؤال دیگری دارم", inbox);
        Assert.Contains("message-unread", inbox);

        // Reception does not see a message for a doctor at all, so cannot reply to it or delete it.
        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        await PostFormAsync(reception, $"/Admin/Messages?handler=Reply&id={messageId}", new() { ["body"] = "پاسخ منشی" });

        await PostFormAsync(staff, $"/Admin/Messages?handler=DeleteReply&replyId={replyId}", []);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var replies = await db.MessageReplies.Where(r => r.ContactMessageId == messageId).ToListAsync();
            Assert.Single(replies);
            Assert.False(replies[0].FromClinic);
        }

        await PostFormAsync(staff, $"/Admin/Messages?handler=Delete&id={messageId}", []);
        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ClinicDbContext>();
        Assert.False(await checkDb.ContactMessages.AnyAsync(m => m.Id == messageId));
        Assert.False(await checkDb.MessageReplies.AnyAsync(r => r.ContactMessageId == messageId));
    }

    [Fact]
    public async Task Record_draws_the_pain_score_under_the_weight_chart()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor), patient);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            db.TreatmentSessions.AddRange(
                new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = DateTime.UtcNow.AddDays(-7), WeightKg = 80, PainScore = 8 },
                new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = DateTime.UtcNow.AddDays(-1), WeightKg = 78, PainScore = 3 });
            await db.SaveChangesAsync();
        }

        var page = await (await LoginAsync(doctor)).GetStringAsync($"/Admin/Records?patientId={patient.Id}");
        var weight = page.IndexOf("روند تغییر وزن", StringComparison.Ordinal) is var w and >= 0 ? w : page.IndexOf("weight-chart", StringComparison.Ordinal);
        var pain = page.IndexOf("pain-chart", StringComparison.Ordinal);
        Assert.True(weight >= 0 && pain > weight);
        Assert.Contains("تغییر نسبت به اولین جلسه: -۵ نمره", page);
    }
}
