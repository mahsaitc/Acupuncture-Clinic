using Clinic.Domain;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Reports;

/// <summary>
/// Visit reports for a day, week, month, year or any date range, for the whole clinic or one patient (by national code),
/// ready to print. A doctor sees only their own visits.
/// </summary>
public class IndexModel(ClinicDbContext db, VisitLog log, StaffScope scope, NationalCodeIndex nationalCodes, IStringLocalizer<SharedResource> l) : PageModel
{
    /// <summary>Day, Week, Month, Year, or Range for the From and To dates.</summary>
    [BindProperty(SupportsGet = true)]
    public string Period { get; set; } = "Day";

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? NationalCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    /// <summary>One patient's visits, for patients without a national code (linked from the patient page).</summary>
    [BindProperty(SupportsGet = true, Name = "Patient")]
    public string? PatientFilter { get; set; }

    public DateOnly RangeFrom { get; private set; }
    public DateOnly RangeTo { get; private set; }
    public string? Error { get; private set; }
    public string? PatientName { get; private set; }
    public string? PatientId { get; private set; }
    public bool CanPickDoctor => !scope.IsOwnOnly;
    public List<(int Id, string Name)> Doctors { get; private set; } = [];
    public List<Row> Rows { get; private set; } = [];
    public HashSet<string> Openable { get; private set; } = [];
    public int PatientCount => Rows.Select(r => r.Visit.PatientUserId).Distinct().Count();
    public List<(string Doctor, int Count)> ByDoctor => Rows.GroupBy(r => r.Doctor).Select(g => (g.Key, g.Count())).OrderByDescending(g => g.Item2).ToList();

    public record Row(Visit Visit, string Patient, string? Phone, string? NationalCode, string Doctor);

    public static readonly string[] PeriodNames = ["Day", "Week", "Month", "Year", "Range"];

    public async Task OnGetAsync()
    {
        Doctors = await scope.DoctorsAsync();
        var today = log.Today;
        var anchor = DisplayFormat.TryParseDateInput(Date, out var d) ? d : today;
        Date = DisplayFormat.DateInput(anchor);
        if (Period == "Range")
        {
            RangeFrom = DisplayFormat.TryParseDateInput(From, out var f) ? f : Periods.Start(today, PeriodKind.Month);
            RangeTo = DisplayFormat.TryParseDateInput(To, out var t) ? t : today;
            if (RangeTo < RangeFrom)
            {
                (RangeFrom, RangeTo) = (RangeTo, RangeFrom);
            }
        }
        else
        {
            var kind = Enum.TryParse<PeriodKind>(Period, out var k) ? k : PeriodKind.Day;
            Period = kind.ToString();
            (RangeFrom, RangeTo) = Periods.Containing(anchor, kind);
        }
        From = DisplayFormat.DateInput(RangeFrom);
        To = DisplayFormat.DateInput(RangeTo);

        string? patientId = null;
        if (!string.IsNullOrWhiteSpace(NationalCode))
        {
            var hash = nationalCodes.Hash(NationalCode);
            patientId = hash is null ? null : await db.Users.Where(u => u.NationalCodeHash == hash).Select(u => u.Id).FirstOrDefaultAsync();
            if (patientId is null || await scope.FindPatientAsync(patientId) is not { } patient)
            {
                Error = l["No patient with this national code was found."];
                return;
            }
            PatientId = patient.Id;
            PatientName = patient.FullName;
        }
        else if (!string.IsNullOrWhiteSpace(PatientFilter))
        {
            if (await scope.FindPatientAsync(PatientFilter) is not { } patient)
            {
                Error = l["Patient not found."];
                return;
            }
            patientId = PatientId = patient.Id;
            PatientName = patient.FullName;
        }

        var visits = await log.BetweenAsync(RangeFrom, RangeTo, patientId, CanPickDoctor ? DoctorId : null);
        var patientIds = visits.Select(v => v.PatientUserId).Distinct().ToList();
        Openable = await scope.OpenablePatientIdsAsync(patientIds);
        var people = await db.Users.AsNoTracking().Where(u => patientIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.PhoneNumber }).ToDictionaryAsync(u => u.Id);
        var codes = (await db.PatientProfiles.AsNoTracking().Where(p => patientIds.Contains(p.UserId)).Select(p => new { p.UserId, p.NationalCode }).ToListAsync())
            .ToDictionary(p => p.UserId, p => p.NationalCode);
        var names = await db.Doctors.AsNoTracking().Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.Id, u.FullName }).ToDictionaryAsync(x => x.Id, x => x.FullName);
        Rows = visits.Select(v => new Row(
                v,
                people.TryGetValue(v.PatientUserId, out var p) ? p.FullName : "-",
                p?.PhoneNumber,
                codes.GetValueOrDefault(v.PatientUserId),
                v.DoctorProfileId is int id && names.TryGetValue(id, out var n) ? n : "-"))
            .ToList();
    }
}
