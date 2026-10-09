using System.Net;
using Clinic.Application.Common;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests;

public partial class ClinicalTests
{
    [Fact]
    public async Task Visit_reports_by_period_and_by_national_code_are_scoped_to_the_doctor()
    {
        var code = NewNationalCode();
        var patient = await CreateUserAsync(Roles.Patient, $"بیمار گزارش {Guid.NewGuid():N}");
        var other = await CreateUserAsync(Roles.Patient, $"بیمار دیگر گزارش {Guid.NewGuid():N}");
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر گزارش"), patient);
        var colleague = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر همکار گزارش"), other);
        DateOnly localToday;
        using (var scope = factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ClinicDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == patient.Id);
            user.NationalCodeHash = sp.GetRequiredService<NationalCodeIndex>().Hash(code);
            db.PatientProfiles.Single(p => p.UserId == patient.Id).NationalCode = code;
            var clinicTime = sp.GetRequiredService<ClinicTime>();
            localToday = clinicTime.Today(DateTime.UtcNow);
            var now = clinicTime.ToUtc(localToday, new TimeOnly(0, 1));
            db.TreatmentSessions.AddRange(
                new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = now, Type = SessionType.CatgutEmbedding },
                new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = now.AddDays(-40), Type = SessionType.Acupuncture },
                new TreatmentSession { PatientUserId = other.Id, DoctorUserId = colleague.Id, DateUtc = now, Type = SessionType.Cupping });
            await db.SaveChangesAsync();
        }

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var today = await reception.GetStringAsync("/Admin/Reports?Period=Day");
        Assert.Contains(patient.FullName, today);
        Assert.Contains(other.FullName, today);
        Assert.Contains(JalaliDate.ToPersianDigits(code), today);

        var from = Uri.EscapeDataString(JalaliDate.ToShortString(localToday.AddDays(-60).ToDateTime(TimeOnly.MinValue)));
        var to = Uri.EscapeDataString(JalaliDate.ToShortString(localToday.ToDateTime(TimeOnly.MinValue)));
        var mine = await reception.GetStringAsync($"/Admin/Reports?Period=Range&From={from}&To={to}&NationalCode={code}");
        Assert.Contains($"مراجعات {patient.FullName}", mine);
        Assert.Contains("کاشت نخ", mine);
        Assert.Contains("طب سوزنی", mine);
        Assert.DoesNotContain(other.FullName, mine);

        var own = await (await LoginAsync(doctor)).GetStringAsync("/Admin/Reports?Period=Month");
        Assert.Contains(patient.FullName, own);
        Assert.DoesNotContain(other.FullName, own);
    }

    [Fact]
    public async Task Admin_sees_clinic_charts_and_reception_does_not()
    {
        var patient = await CreateUserAsync(Roles.Patient);
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر نمودار"), patient);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var profile = db.PatientProfiles.Single(p => p.UserId == patient.Id);
            profile.Gender = Gender.Female;
            profile.BirthDate = new DateOnly(1990, 1, 1);
            profile.Education = "Bachelor's degree";
            db.TreatmentSessions.AddRange(
                new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = DateTime.UtcNow.AddDays(-20), PainScore = 8 },
                new TreatmentSession { PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = DateTime.UtcNow.AddMinutes(-5), PainScore = 3 });
            await db.SaveChangesAsync();
        }

        var admin = await LoginAsync(await CreateUserAsync(Roles.Admin));
        foreach (var grain in new[] { "Week", "Month", "Year" })
        {
            var response = await admin.GetAsync($"/Admin/Reports/Charts?Grain={grain}");
            var page = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, page[..Math.Min(4000, page.Length)]);
            Assert.Contains("chartjs/chart.umd.min", page);
            Assert.Contains("مراجعات به تفکیک جنس", page);
            Assert.Contains("زن", page);
            Assert.Contains("لیسانس", page);
            Assert.Contains("دکتر نمودار", page);
        }

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        Assert.StartsWith("/Account/AccessDenied", (await reception.GetAsync("/Admin/Reports/Charts")).Headers.Location!.PathAndQuery);
    }

    [Fact]
    public void Periods_follow_the_persian_calendar()
    {
        // 1405/07/17 is Friday 9 October 2026.
        var day = new DateOnly(2026, 10, 9);
        Assert.Equal(new DateOnly(2026, 10, 3), Periods.Start(day, PeriodKind.Week));
        Assert.Equal(new DateOnly(2026, 9, 23), Periods.Start(day, PeriodKind.Month));
        Assert.Equal(new DateOnly(2026, 3, 21), Periods.Start(day, PeriodKind.Year));
        Assert.Equal((new DateOnly(2026, 9, 23), new DateOnly(2026, 10, 22)), Periods.Containing(day, PeriodKind.Month));
        Assert.Equal("مهر ۱۴۰۵", Periods.Label(new DateOnly(2026, 9, 23), PeriodKind.Month, english: false));
    }

    [Fact]
    public async Task Home_page_shows_testimonials_highlights_and_satisfaction()
    {
        var page = await factory.CreateClient().GetStringAsync("/");
        Assert.Contains("id=\"testimonials\"", page);
        Assert.Contains("id=\"why-us\"", page);
        Assert.Contains("میانگین رضایت بیماران بر اساس نوع بیماری", page);
        Assert.Contains("میانگین رضایت بیماران بر اساس نوع درمان", page);
    }

    [Fact]
    public async Task Reception_finds_a_patient_by_national_code_and_sees_their_visits()
    {
        var code = NewNationalCode();
        var patient = await CreateUserAsync(Roles.Patient, $"بیمار کد ملی {Guid.NewGuid():N}");
        var doctor = await AssignAsync(await CreateUserAsync(Roles.Doctor, "دکتر کد ملی"), patient);
        using (var scope = factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ClinicDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == patient.Id);
            user.NationalCodeHash = sp.GetRequiredService<NationalCodeIndex>().Hash(code);
            db.PatientProfiles.Single(p => p.UserId == patient.Id).NationalCode = code;
            db.TreatmentSessions.Add(new TreatmentSession
            {
                PatientUserId = patient.Id, DoctorUserId = doctor.Id, DateUtc = DateTime.UtcNow.AddDays(-3), Type = SessionType.CatgutEmbedding,
            });
            await db.SaveChangesAsync();
        }

        var reception = await LoginAsync(await CreateUserAsync(Roles.Receptionist));
        var list = await reception.GetStringAsync($"/Admin/Patients?Q={Uri.EscapeDataString(patient.FullName)}");
        Assert.Contains(JalaliDate.ToPersianDigits(code), list);

        // Searching the code (typed with Persian digits) opens the patient's page with their visits.
        var response = await reception.GetAsync($"/Admin/Patients?Q={Uri.EscapeDataString(JalaliDate.ToPersianDigits(code))}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Admin/Patients/Details", response.Headers.Location!.OriginalString);
        var page = await reception.GetStringAsync(response.Headers.Location);
        Assert.Contains(patient.FullName, page);
        Assert.Contains("کاشت نخ", page);
        Assert.Contains($"Patient={patient.Id}", page);

        var printed = await reception.GetStringAsync($"/Admin/Reports?Period=Range&Patient={patient.Id}&From=1400/01/01");
        Assert.Contains($"مراجعات {patient.FullName}", printed);
        Assert.Contains("کاشت نخ", printed);
    }
}
