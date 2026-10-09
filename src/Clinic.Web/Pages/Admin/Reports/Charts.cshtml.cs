using System.Text.Json;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Clinical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Reports;

/// <summary>
/// The admin's charts of the clinic's visits over weeks, months or years: by sex, age, education, occupation, treatment
/// and doctor, plus the average pain score and pain reduction. Each has a bar chart over time and a pie of the totals.
/// </summary>
[Authorize(Policy = Policies.Admin)]
public class ChartsModel(ClinicDbContext db, VisitLog log, Clinic.Web.Localization.DisplayFormat fmt, IStringLocalizer<SharedResource> l) : PageModel
{
    public static readonly PeriodKind[] Grains = [PeriodKind.Week, PeriodKind.Month, PeriodKind.Year];

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public PeriodKind Grain { get; set; } = PeriodKind.Month;

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public int Count { get; set; }

    public int VisitCount { get; private set; }
    public string RangeText { get; private set; } = "";

    /// <summary>Everything the page's script draws, as JSON.</summary>
    public string ChartsJson { get; private set; } = "{}";

    public sealed record Series(string Name, double[] Values);

    public sealed record Breakdown(string Key, string Title, string[] Periods, List<Series> Series, double[] Totals);

    public async Task OnGetAsync()
    {
        if (!Grains.Contains(Grain))
        {
            Grain = PeriodKind.Month;
        }
        Count = Math.Clamp(Count <= 0 ? (Grain == PeriodKind.Year ? 5 : 12) : Count, 2, Grain == PeriodKind.Week ? 52 : 36);
        var english = Clinic.Web.Localization.CulturePath.IsEnglish;
        var starts = Periods.Last(log.Today, Grain, Count);
        var to = Periods.Next(starts[^1], Grain).AddDays(-1);
        var visits = await log.BetweenAsync(starts[0], to);
        VisitCount = visits.Count;
        RangeText = $"{fmt.Day(starts[0])} - {fmt.Day(to)}";
        var labels = starts.Select(s => Periods.Label(s, Grain, english)).ToArray();
        int PeriodOf(Visit v)
        {
            var i = starts.FindLastIndex(s => s <= v.Day);
            return Math.Max(0, i);
        }

        var ids = visits.Select(v => v.PatientUserId).Distinct().ToList();
        var profiles = await db.PatientProfiles.AsNoTracking().Where(p => ids.Contains(p.UserId))
            .Select(p => new { p.UserId, p.Gender, p.BirthDate, p.Education, p.Occupation })
            .ToDictionaryAsync(p => p.UserId);
        var doctors = await db.Doctors.AsNoTracking().Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d.Id, u.FullName }).ToDictionaryAsync(d => d.Id, d => d.FullName);
        string unknown = l["Not recorded"];

        Breakdown By(string key, string title, Func<Visit, string> category, IEnumerable<string>? order = null)
        {
            var groups = visits.GroupBy(category).ToDictionary(g => g.Key, g => g.ToList());
            var names = (order ?? []).Where(groups.ContainsKey).Concat(groups.Keys.Where(k => !(order ?? []).Contains(k)).OrderBy(k => k == unknown)).Distinct().ToList();
            var series = names.Select(n =>
            {
                var values = new double[starts.Count];
                foreach (var v in groups[n])
                {
                    values[PeriodOf(v)]++;
                }
                return new Series(n, values);
            }).ToList();
            return new Breakdown(key, title, labels, series, series.Select(s => s.Values.Sum()).ToArray());
        }

        string Age(Visit v)
        {
            if (profiles.GetValueOrDefault(v.PatientUserId)?.BirthDate is not DateOnly birth)
            {
                return unknown;
            }
            var age = v.Day.Year - birth.Year - (v.Day < birth.AddYears(v.Day.Year - birth.Year) ? 1 : 0);
            return age switch
            {
                < 18 => l["Under 18"],
                < 30 => l["18 to 29"],
                < 40 => l["30 to 39"],
                < 50 => l["40 to 49"],
                < 60 => l["50 to 59"],
                _ => l["60 and over"],
            };
        }

        string Listed(string? key) => string.IsNullOrWhiteSpace(key) ? unknown : l[key];

        var total = new Breakdown("total", l["All visits"], labels, [new Series(l["Visits"], CountPer(visits))], [visits.Count]);
        var breakdowns = new List<Breakdown>
        {
            total,
            By("sex", l["Visits by sex"], v => profiles.GetValueOrDefault(v.PatientUserId)?.Gender is Gender g ? l[g.ToString()] : unknown),
            By("age", l["Visits by age group"], Age, [l["Under 18"], l["18 to 29"], l["30 to 39"], l["40 to 49"], l["50 to 59"], l["60 and over"]]),
            By("education", l["Visits by education"], v => Listed(profiles.GetValueOrDefault(v.PatientUserId)?.Education),
                Clinic.Web.Pages.Admin.Patients.PatientInput.EducationLevels.Select(e => l[e].Value)),
            By("occupation", l["Visits by occupation"], v => Listed(profiles.GetValueOrDefault(v.PatientUserId)?.Occupation),
                Clinic.Web.Pages.Admin.Patients.PatientInput.Occupations.Select(e => l[e].Value)),
            By("treatment", l["Visits by treatment"], v => v.Treatment is SessionType t ? l[t.ToString()] : l["No session recorded"]),
            By("doctor", l["Visits by doctor"], v => v.DoctorProfileId is int d && doctors.TryGetValue(d, out var n) ? n : unknown),
        };

        // Pain: the average score of the sessions in each period, and how far below each patient's first recorded score it is.
        var firstPain = (await db.TreatmentSessions.AsNoTracking().Where(s => ids.Contains(s.PatientUserId) && s.PainScore != null)
                .Select(s => new { s.PatientUserId, s.DateUtc, s.PainScore }).ToListAsync())
            .GroupBy(s => s.PatientUserId)
            .ToDictionary(g => g.Key, g => (double)g.OrderBy(s => s.DateUtc).First().PainScore!.Value);
        var avgPain = new double[starts.Count];
        var avgDrop = new double[starts.Count];
        for (var i = 0; i < starts.Count; i++)
        {
            var scored = visits.Where(v => v.PainScore is not null && PeriodOf(v) == i).ToList();
            avgPain[i] = scored.Count == 0 ? 0 : Math.Round(scored.Average(v => v.PainScore!.Value), 1);
            avgDrop[i] = scored.Count == 0 ? 0 : Math.Round(scored.Average(v => firstPain.GetValueOrDefault(v.PatientUserId, v.PainScore!.Value) - v.PainScore!.Value), 1);
        }
        var pain = new Breakdown("pain", l["Pain score and pain reduction"], labels,
            [new Series(l["Average pain score (0 to 10)"], avgPain), new Series(l["Average reduction since the first session"], avgDrop)], []);

        ChartsJson = JsonSerializer.Serialize(new { breakdowns, pain }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All) });

        double[] CountPer(List<Visit> list)
        {
            var values = new double[starts.Count];
            foreach (var v in list)
            {
                values[PeriodOf(v)]++;
            }
            return values;
        }
    }
}
