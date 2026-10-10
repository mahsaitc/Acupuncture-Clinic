using System.ComponentModel.DataAnnotations;
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
public class EditModel(ClinicDbContext db, UserManager<ApplicationUser> users, AuditLog audit, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    public ApplicationUser Patient { get; private set; } = default!;
    public PatientProfile? Profile { get; private set; }
    public bool IsNew { get; private set; }
    public int? Age { get; private set; }

    [BindProperty]
    public RecordInput Input { get; set; } = new();

    public class RecordInput
    {
        // 2. Reason for the visit.
        [StringLength(2000)]
        [Display(Name = "Chief complaint")]
        public string? ChiefComplaint { get; set; }

        [StringLength(200)]
        [Display(Name = "How long has it been going on?")]
        public string? ProblemDuration { get; set; }

        public List<TreatmentGoal> Goals { get; set; } = [];

        [StringLength(2000)]
        [Display(Name = "Other goals")]
        public string? GoalsNotes { get; set; }

        // 3. History.
        public List<MedicalCondition> Conditions { get; set; } = [];

        [StringLength(2000)]
        [Display(Name = "Other conditions")]
        public string? OtherConditions { get; set; }

        [StringLength(2000)]
        [Display(Name = "Current medications")]
        public string? Medications { get; set; }

        [StringLength(2000)]
        [Display(Name = "Surgeries")]
        public string? Surgeries { get; set; }

        public bool? HasDrugAllergy { get; set; }

        [StringLength(2000)]
        [Display(Name = "Drug or substance")]
        public string? DrugAllergies { get; set; }

        public bool? HasOtherAllergy { get; set; }

        [StringLength(2000)]
        [Display(Name = "Substance")]
        public string? OtherAllergies { get; set; }

        // 4. General state.
        public SleepQuality? Sleep { get; set; }
        public AppetiteLevel? Appetite { get; set; }
        public List<TasteCraving> Cravings { get; set; } = [];
        public List<DigestionState> Digestion { get; set; } = [];
        public EnergyLevel? Energy { get; set; }
        public List<MenstrualState> Menstrual { get; set; } = [];

        [StringLength(20)]
        [Display(Name = "Last menstrual period")]
        public string? LastMenstrualPeriod { get; set; }

        public List<Mood> Mood { get; set; } = [];

        // 5. Measurements.
        [Range(40, 250, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Height (cm)")]
        public double? HeightCm { get; set; }

        [Range(2, 400, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Weight before treatment (kg)")]
        public double? WeightKg { get; set; }

        [Range(2, 400, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Weight after treatment (kg)")]
        public double? WeightAfterKg { get; set; }

        [Range(30, 250, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Waist (cm)")]
        public double? WaistCm { get; set; }

        [Range(30, 250, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Hips (cm)")]
        public double? HipCm { get; set; }

        [Range(10, 150, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Thigh (cm)")]
        public double? ThighCm { get; set; }

        [Range(5, 100, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Arm (cm)")]
        public double? ArmCm { get; set; }

        [StringLength(2000)]
        [Display(Name = "Changes observed")]
        public string? ObservedChanges { get; set; }

        // 6. TCM diagnosis.
        public List<PulseQuality> Pulse { get; set; } = [];

        [StringLength(200)]
        [Display(Name = "Tongue colour")]
        public string? TongueColor { get; set; }

        [StringLength(200)]
        [Display(Name = "Tongue coating")]
        public string? TongueCoating { get; set; }

        [StringLength(1000)]
        [Display(Name = "Type of imbalance")]
        public string? TcmPattern { get; set; }

        [StringLength(50)]
        [Display(Name = "ICD-10 code")]
        public string? Icd10 { get; set; }

        [StringLength(4000)]
        [Display(Name = "Medical diagnosis")]
        public string? Diagnosis { get; set; }

        // 7. Treatment plan.
        [StringLength(2000)]
        [Display(Name = "Short-term goals")]
        public string? ShortTermGoals { get; set; }

        [StringLength(2000)]
        [Display(Name = "Long-term goals")]
        public string? LongTermGoals { get; set; }

        public List<TreatmentArea> Areas { get; set; } = [];

        [StringLength(500)]
        [Display(Name = "Other areas")]
        public string? OtherAreas { get; set; }

        public List<TreatmentMethod> Methods { get; set; } = [];

        [Range(1, 200, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Suggested number of sessions")]
        public int? PlannedSessions { get; set; }

        [StringLength(200)]
        [Display(Name = "Time between sessions")]
        public string? SessionInterval { get; set; }

        [StringLength(2000)]
        [Display(Name = "Advice after treatment")]
        public string? AfterCareAdvice { get; set; }

        // 9 and 10.
        [Display(Name = "The patient signed the treatment consent form")]
        public bool ConsentSigned { get; set; }

        [StringLength(8000)]
        [Display(Name = "Doctor's notes")]
        public string? DoctorNotes { get; set; }

        public static RecordInput From(MedicalRecord r) => new()
        {
            ChiefComplaint = r.ChiefComplaint,
            ProblemDuration = r.ProblemDuration,
            Goals = CheckGroup.Split(r.Goals),
            GoalsNotes = r.GoalsNotes,
            Conditions = CheckGroup.Split(r.Conditions),
            OtherConditions = r.OtherConditions,
            Medications = r.Medications,
            Surgeries = r.Surgeries,
            HasDrugAllergy = r.HasDrugAllergy,
            DrugAllergies = r.DrugAllergies,
            HasOtherAllergy = r.HasOtherAllergy,
            OtherAllergies = r.OtherAllergies,
            Sleep = r.Sleep,
            Appetite = r.Appetite,
            Cravings = CheckGroup.Split(r.Cravings),
            Digestion = CheckGroup.Split(r.Digestion),
            Energy = r.Energy,
            Menstrual = CheckGroup.Split(r.Menstrual),
            LastMenstrualPeriod = DisplayFormat.DateInput(r.LastMenstrualPeriod),
            Mood = CheckGroup.Split(r.Mood),
            HeightCm = r.HeightCm,
            WeightKg = r.WeightKg,
            WeightAfterKg = r.WeightAfterKg,
            WaistCm = r.WaistCm,
            HipCm = r.HipCm,
            ThighCm = r.ThighCm,
            ArmCm = r.ArmCm,
            ObservedChanges = r.ObservedChanges,
            Pulse = CheckGroup.Split(r.Pulse),
            TongueColor = r.TongueColor,
            TongueCoating = r.TongueCoating,
            TcmPattern = r.TcmPattern,
            Icd10 = r.Icd10,
            Diagnosis = r.Diagnosis,
            ShortTermGoals = r.ShortTermGoals,
            LongTermGoals = r.LongTermGoals,
            Areas = CheckGroup.Split(r.Areas),
            OtherAreas = r.OtherAreas,
            Methods = CheckGroup.Split(r.Methods),
            PlannedSessions = r.PlannedSessions,
            SessionInterval = r.SessionInterval,
            AfterCareAdvice = r.AfterCareAdvice,
            ConsentSigned = r.ConsentSignedUtc is not null,
            DoctorNotes = r.DoctorNotes,
        };

        public void ApplyTo(MedicalRecord r, DateOnly? lastPeriod)
        {
            r.ChiefComplaint = Clean(ChiefComplaint);
            r.ProblemDuration = Clean(ProblemDuration);
            r.Goals = CheckGroup.Combine(Goals);
            r.GoalsNotes = Clean(GoalsNotes);
            r.Conditions = CheckGroup.Combine(Conditions);
            r.OtherConditions = Clean(OtherConditions);
            r.Medications = Clean(Medications);
            r.Surgeries = Clean(Surgeries);
            r.HasDrugAllergy = HasDrugAllergy;
            r.DrugAllergies = Clean(DrugAllergies);
            r.HasOtherAllergy = HasOtherAllergy;
            r.OtherAllergies = Clean(OtherAllergies);
            r.Sleep = Sleep;
            r.Appetite = Appetite;
            r.Cravings = CheckGroup.Combine(Cravings);
            r.Digestion = CheckGroup.Combine(Digestion);
            r.Energy = Energy;
            r.Menstrual = CheckGroup.Combine(Menstrual);
            r.LastMenstrualPeriod = lastPeriod;
            r.Mood = CheckGroup.Combine(Mood);
            r.HeightCm = HeightCm;
            r.WeightKg = WeightKg;
            r.WeightAfterKg = WeightAfterKg;
            r.WaistCm = WaistCm;
            r.HipCm = HipCm;
            r.ThighCm = ThighCm;
            r.ArmCm = ArmCm;
            r.ObservedChanges = Clean(ObservedChanges);
            r.Pulse = CheckGroup.Combine(Pulse);
            r.TongueColor = Clean(TongueColor);
            r.TongueCoating = Clean(TongueCoating);
            r.TcmPattern = Clean(TcmPattern);
            r.Icd10 = Clean(Icd10)?.ToUpperInvariant();
            r.Diagnosis = Clean(Diagnosis);
            r.ShortTermGoals = Clean(ShortTermGoals);
            r.LongTermGoals = Clean(LongTermGoals);
            r.Areas = CheckGroup.Combine(Areas);
            r.OtherAreas = Clean(OtherAreas);
            r.Methods = CheckGroup.Combine(Methods);
            r.PlannedSessions = PlannedSessions;
            r.SessionInterval = Clean(SessionInterval);
            r.AfterCareAdvice = Clean(AfterCareAdvice);
            r.DoctorNotes = Clean(DoctorNotes);
        }
    }

    public async Task<IActionResult> OnGetAsync(string patientId)
    {
        if (!await LoadPatientAsync(patientId))
        {
            return NotFound();
        }

        var record = await db.MedicalRecords.AsNoTracking().FirstOrDefaultAsync(r => r.PatientUserId == patientId);
        IsNew = record is null;
        if (record is not null)
        {
            Input = RecordInput.From(record);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string patientId)
    {
        if (!await LoadPatientAsync(patientId))
        {
            return NotFound();
        }

        var record = await db.MedicalRecords.FirstOrDefaultAsync(r => r.PatientUserId == patientId);
        IsNew = record is null;

        DateOnly? lastPeriod = null;
        if (!string.IsNullOrWhiteSpace(Input.LastMenstrualPeriod))
        {
            if (DisplayFormat.TryParseDateInput(Input.LastMenstrualPeriod, out var parsed))
            {
                lastPeriod = parsed;
            }
            else
            {
                ModelState.AddModelError("Input.LastMenstrualPeriod", l["Enter the date like {0}.", DisplayFormat.DateInputHint]);
            }
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (record is null)
        {
            record = new MedicalRecord { PatientUserId = patientId, CreatedUtc = now };
            db.MedicalRecords.Add(record);
        }

        Input.ApplyTo(record, lastPeriod);
        record.ConsentSignedUtc = Input.ConsentSigned ? record.ConsentSignedUtc ?? now : null;
        record.UpdatedUtc = now;

        audit.Add(AuditAction.UpdateRecord, patientId);
        await db.SaveChangesAsync();

        TempData["Message"] = l["The medical record was saved."].Value;
        return RedirectToPage("./Index", new { patientId });
    }

    private async Task<bool> LoadPatientAsync(string patientId)
    {
        var patient = await RecordHeader.FindPatientAsync(users, patientId);
        if (patient is null)
        {
            return false;
        }
        Patient = patient;
        Profile = await db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == patientId);
        Age = Profile?.AgeOn(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
        return true;
    }

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
