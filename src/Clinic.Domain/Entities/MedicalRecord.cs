namespace Clinic.Domain.Entities;

public enum Gender
{
    Female = 0,
    Male = 1,
}

/// <summary>One medical record per patient, written by the doctor.</summary>
public class MedicalRecord
{
    public int Id { get; set; }
    public string PatientUserId { get; set; } = default!;

    // Identity and background. NationalCode is encrypted at rest.
    public string? NationalCode { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public string? Occupation { get; set; }
    public string? Address { get; set; }
    public string? ReferralSource { get; set; }

    // History.
    public string? ChiefComplaint { get; set; }
    public string? PastMedicalHistory { get; set; }
    public string? Surgeries { get; set; }
    public string? Medications { get; set; }
    public string? Allergies { get; set; }
    public string? FamilyHistory { get; set; }

    // Baseline measurements, taken at the first visit.
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public double? WaistCm { get; set; }
    public string? BloodPressure { get; set; }
    public int? Pulse { get; set; }

    // Traditional assessment.
    public string? PulseDiagnosis { get; set; }
    public string? TongueDiagnosis { get; set; }
    public string? TcmPattern { get; set; }

    // Diagnosis is encrypted at rest.
    public string? Icd10 { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentPlan { get; set; }

    public DateTime? ConsentSignedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public double? Bmi => HeightCm is > 0 && WeightKg is > 0 ? Math.Round(WeightKg.Value / Math.Pow(HeightCm.Value / 100, 2), 1) : null;
}
