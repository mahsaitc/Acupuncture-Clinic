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
    public bool IsNew { get; private set; }

    [BindProperty]
    public RecordInput Input { get; set; } = new();

    public class RecordInput
    {
        [StringLength(10, MinimumLength = 10, ErrorMessage = "The national code has 10 digits.")]
        [RegularExpression("^[0-9۰-۹]{10}$", ErrorMessage = "The national code has 10 digits.")]
        [Display(Name = "National code")]
        public string? NationalCode { get; set; }

        [Display(Name = "Birth date")]
        public string? BirthDate { get; set; }

        [Display(Name = "Gender")]
        public Gender? Gender { get; set; }

        [StringLength(100)]
        [Display(Name = "Occupation")]
        public string? Occupation { get; set; }

        [StringLength(300)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [StringLength(100)]
        [Display(Name = "Referred by")]
        public string? ReferralSource { get; set; }

        [StringLength(2000)]
        [Display(Name = "Chief complaint")]
        public string? ChiefComplaint { get; set; }

        [StringLength(4000)]
        [Display(Name = "Past medical history")]
        public string? PastMedicalHistory { get; set; }

        [StringLength(2000)]
        [Display(Name = "Surgeries")]
        public string? Surgeries { get; set; }

        [StringLength(2000)]
        [Display(Name = "Current medications")]
        public string? Medications { get; set; }

        [StringLength(1000)]
        [Display(Name = "Allergies")]
        public string? Allergies { get; set; }

        [StringLength(2000)]
        [Display(Name = "Family history")]
        public string? FamilyHistory { get; set; }

        [Range(40, 250, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Height (cm)")]
        public double? HeightCm { get; set; }

        [Range(2, 400, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Weight (kg)")]
        public double? WeightKg { get; set; }

        [Range(30, 250, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Waist (cm)")]
        public double? WaistCm { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\s*[0-9۰-۹]{2,3}\s*/\s*[0-9۰-۹]{2,3}\s*$", ErrorMessage = "Write blood pressure like 120/80.")]
        [Display(Name = "Blood pressure")]
        public string? BloodPressure { get; set; }

        [Range(20, 250, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Heart rate")]
        public int? Pulse { get; set; }

        [StringLength(500)]
        [Display(Name = "Pulse diagnosis")]
        public string? PulseDiagnosis { get; set; }

        [StringLength(500)]
        [Display(Name = "Tongue diagnosis")]
        public string? TongueDiagnosis { get; set; }

        [StringLength(500)]
        [Display(Name = "TCM pattern")]
        public string? TcmPattern { get; set; }

        [StringLength(50)]
        [Display(Name = "ICD-10 code")]
        public string? Icd10 { get; set; }

        [StringLength(4000)]
        [Display(Name = "Diagnosis")]
        public string? Diagnosis { get; set; }

        [StringLength(4000)]
        [Display(Name = "Treatment plan")]
        public string? TreatmentPlan { get; set; }

        [Display(Name = "The patient signed the treatment consent form")]
        public bool ConsentSigned { get; set; }
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
            Input = new RecordInput
            {
                NationalCode = record.NationalCode,
                BirthDate = DisplayFormat.DateInput(record.BirthDate),
                Gender = record.Gender,
                Occupation = record.Occupation,
                Address = record.Address,
                ReferralSource = record.ReferralSource,
                ChiefComplaint = record.ChiefComplaint,
                PastMedicalHistory = record.PastMedicalHistory,
                Surgeries = record.Surgeries,
                Medications = record.Medications,
                Allergies = record.Allergies,
                FamilyHistory = record.FamilyHistory,
                HeightCm = record.HeightCm,
                WeightKg = record.WeightKg,
                WaistCm = record.WaistCm,
                BloodPressure = record.BloodPressure,
                Pulse = record.Pulse,
                PulseDiagnosis = record.PulseDiagnosis,
                TongueDiagnosis = record.TongueDiagnosis,
                TcmPattern = record.TcmPattern,
                Icd10 = record.Icd10,
                Diagnosis = record.Diagnosis,
                TreatmentPlan = record.TreatmentPlan,
                ConsentSigned = record.ConsentSignedUtc is not null,
            };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string patientId)
    {
        if (!await LoadPatientAsync(patientId))
        {
            return NotFound();
        }

        DateOnly? birth = null;
        if (!string.IsNullOrWhiteSpace(Input.BirthDate))
        {
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            if (!DisplayFormat.TryParseDateInput(Input.BirthDate, out var parsed) || parsed > today || parsed.Year < today.Year - 120)
            {
                ModelState.AddModelError("Input.BirthDate", l["Enter the date like {0}.", DisplayFormat.DateInputHint]);
            }
            else
            {
                birth = parsed;
            }
        }

        var record = await db.MedicalRecords.FirstOrDefaultAsync(r => r.PatientUserId == patientId);
        IsNew = record is null;
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

        record.NationalCode = Clean(Clinic.Application.Common.JalaliDate.ToLatinDigits(Input.NationalCode ?? ""));
        record.BirthDate = birth;
        record.Gender = Input.Gender;
        record.Occupation = Clean(Input.Occupation);
        record.Address = Clean(Input.Address);
        record.ReferralSource = Clean(Input.ReferralSource);
        record.ChiefComplaint = Clean(Input.ChiefComplaint);
        record.PastMedicalHistory = Clean(Input.PastMedicalHistory);
        record.Surgeries = Clean(Input.Surgeries);
        record.Medications = Clean(Input.Medications);
        record.Allergies = Clean(Input.Allergies);
        record.FamilyHistory = Clean(Input.FamilyHistory);
        record.HeightCm = Input.HeightCm;
        record.WeightKg = Input.WeightKg;
        record.WaistCm = Input.WaistCm;
        record.BloodPressure = Clean(Clinic.Application.Common.JalaliDate.ToLatinDigits(Input.BloodPressure ?? "").Replace(" ", ""));
        record.Pulse = Input.Pulse;
        record.PulseDiagnosis = Clean(Input.PulseDiagnosis);
        record.TongueDiagnosis = Clean(Input.TongueDiagnosis);
        record.TcmPattern = Clean(Input.TcmPattern);
        record.Icd10 = Clean(Input.Icd10)?.ToUpperInvariant();
        record.Diagnosis = Clean(Input.Diagnosis);
        record.TreatmentPlan = Clean(Input.TreatmentPlan);
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
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
