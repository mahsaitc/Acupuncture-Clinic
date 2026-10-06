using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Clinic.Application.Common;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Clinical;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Records;

[Authorize(Policy = Policies.Doctor)]
public class SessionModel(
    ClinicDbContext db,
    UserManager<ApplicationUser> users,
    AuditLog audit,
    ClinicTime clinicTime,
    TimeProvider time,
    IStringLocalizer<SharedResource> l) : PageModel
{
    public ApplicationUser Patient { get; private set; } = default!;

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public SessionInput Input { get; set; } = new();

    /// <summary>Points of the patient's latest earlier session, for "copy from last session".</summary>
    public string? PreviousPointsJson { get; private set; }
    public DateTime? PreviousDateUtc { get; private set; }

    public class SessionInput
    {
        [Required(ErrorMessage = "{0} is required.")]
        [Display(Name = "Date")]
        public string Date { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [RegularExpression("^([01]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Write the time like 14:30.")]
        [Display(Name = "Time")]
        public string Time { get; set; } = "";

        [Display(Name = "Treatment type")]
        public SessionType Type { get; set; }

        [Range(2, 400, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Weight (kg)")]
        public double? WeightKg { get; set; }

        [Range(30, 250, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Waist (cm)")]
        public double? WaistCm { get; set; }

        [Range(0, 10, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Pain score (0 to 10)")]
        public int? PainScore { get; set; }

        [Range(1, 180, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Needle retention (minutes)")]
        public int? NeedleRetentionMinutes { get; set; }

        [StringLength(2000)]
        [Display(Name = "Complaint today")]
        public string? Complaint { get; set; }

        [StringLength(2000)]
        [Display(Name = "Reactions and side effects")]
        public string? Reactions { get; set; }

        [StringLength(4000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(2000)]
        [Display(Name = "Plan for next session")]
        public string? NextPlan { get; set; }

        public string? PointsJson { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string patientId)
    {
        if (!await LoadAsync(patientId))
        {
            return NotFound();
        }

        if (Id is null)
        {
            var local = clinicTime.ToLocal(time.GetUtcNow().UtcDateTime);
            Input = new SessionInput
            {
                Date = DisplayFormat.DateInput(DateOnly.FromDateTime(local)),
                Time = local.ToString("HH:mm", CultureInfo.InvariantCulture),
                PointsJson = "[]",
            };
            return Page();
        }

        var session = await db.TreatmentSessions.AsNoTracking().Include(s => s.Points)
            .FirstOrDefaultAsync(s => s.Id == Id && s.PatientUserId == patientId);
        if (session is null)
        {
            return NotFound();
        }

        var start = clinicTime.ToLocal(session.DateUtc);
        Input = new SessionInput
        {
            Date = DisplayFormat.DateInput(DateOnly.FromDateTime(start)),
            Time = start.ToString("HH:mm", CultureInfo.InvariantCulture),
            Type = session.Type,
            WeightKg = session.WeightKg,
            WaistCm = session.WaistCm,
            PainScore = session.PainScore,
            NeedleRetentionMinutes = session.NeedleRetentionMinutes,
            Complaint = session.Complaint,
            Reactions = session.Reactions,
            Notes = session.Notes,
            NextPlan = session.NextPlan,
            PointsJson = BodyMapPoints.ToJson(session.Points.OrderBy(p => p.Id)),
        };
        await audit.WriteAsync(AuditAction.ViewRecord, patientId, session.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string patientId)
    {
        if (!await LoadAsync(patientId))
        {
            return NotFound();
        }

        TreatmentSession? session = null;
        if (Id is not null)
        {
            session = await db.TreatmentSessions.Include(s => s.Points).FirstOrDefaultAsync(s => s.Id == Id && s.PatientUserId == patientId);
            if (session is null)
            {
                return NotFound();
            }
        }

        if (!DisplayFormat.TryParseDateInput(Input.Date, out var date))
        {
            ModelState.AddModelError("Input.Date", l["Enter the date like {0}.", DisplayFormat.DateInputHint]);
        }
        var points = BodyMapPoints.Parse(Input.PointsJson);
        if (points is null)
        {
            ModelState.AddModelError(string.Empty, l["The points on the diagram could not be read. Please try again."]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = time.GetUtcNow().UtcDateTime;
        var isNew = session is null;
        if (session is null)
        {
            session = new TreatmentSession
            {
                PatientUserId = patientId,
                DoctorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                CreatedUtc = now,
            };
            db.TreatmentSessions.Add(session);
        }

        session.DateUtc = clinicTime.ToUtc(date, TimeOnly.ParseExact(Input.Time.PadLeft(5, '0'), "HH:mm", CultureInfo.InvariantCulture));
        session.Type = Input.Type;
        session.WeightKg = Input.WeightKg;
        session.WaistCm = Input.WaistCm;
        session.PainScore = Input.PainScore;
        session.NeedleRetentionMinutes = Input.NeedleRetentionMinutes;
        session.Complaint = Clean(Input.Complaint);
        session.Reactions = Clean(Input.Reactions);
        session.Notes = Clean(Input.Notes);
        session.NextPlan = Clean(Input.NextPlan);
        session.UpdatedUtc = now;
        session.Points.Clear();
        session.Points.AddRange(points!);

        await db.SaveChangesAsync();
        audit.Add(isNew ? AuditAction.CreateSession : AuditAction.UpdateSession, patientId, session.Id);
        await db.SaveChangesAsync();

        TempData["Message"] = l["The session was saved."].Value;
        return RedirectToPage("./Session", new { patientId, id = session.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string patientId)
    {
        var session = await db.TreatmentSessions.FirstOrDefaultAsync(s => s.Id == Id && s.PatientUserId == patientId);
        if (session is null)
        {
            return NotFound();
        }
        db.TreatmentSessions.Remove(session);
        audit.Add(AuditAction.DeleteSession, patientId, session.Id);
        await db.SaveChangesAsync();

        TempData["Message"] = l["The session was deleted."].Value;
        return RedirectToPage("./Index", new { patientId });
    }

    private async Task<bool> LoadAsync(string patientId)
    {
        var patient = await RecordHeader.FindPatientAsync(users, patientId);
        if (patient is null)
        {
            return false;
        }
        Patient = patient;

        var previous = await db.TreatmentSessions.AsNoTracking().Include(s => s.Points)
            .Where(s => s.PatientUserId == patientId && s.Id != Id && s.Points.Count > 0)
            .OrderByDescending(s => s.DateUtc)
            .FirstOrDefaultAsync();
        if (previous is not null)
        {
            PreviousPointsJson = BodyMapPoints.ToJson(previous.Points.OrderBy(p => p.Id));
            PreviousDateUtc = previous.DateUtc;
        }
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
