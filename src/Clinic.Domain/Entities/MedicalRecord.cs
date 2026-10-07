namespace Clinic.Domain.Entities;

public enum Gender
{
    Female = 0,
    Male = 1,
}

// The checklists of the clinic's intake form. Flags so that several boxes can be ticked.

[Flags]
public enum TreatmentGoal
{
    None = 0,
    WeightLoss = 1,
    Pain = 2,
    Anxiety = 4,
    SkinAndHair = 8,
}

[Flags]
public enum MedicalCondition
{
    None = 0,
    Diabetes = 1 << 0,
    Hypertension = 1 << 1,
    Thyroid = 1 << 2,
    Hyperlipidemia = 1 << 3,
    Anemia = 1 << 4,
    HeartDisease = 1 << 5,
    Migraine = 1 << 6,
    JointPain = 1 << 7,
    DiscProblem = 1 << 8,
    Allergy = 1 << 9,
    Depression = 1 << 10,
    Anxiety = 1 << 11,
    Insomnia = 1 << 12,
    Constipation = 1 << 13,
    Indigestion = 1 << 14,
    HormonalProblem = 1 << 15,
    Infertility = 1 << 16,
}

public enum SleepQuality
{
    Good = 0,
    Fair = 1,
    Poor = 2,
}

public enum AppetiteLevel
{
    Normal = 0,
    Increased = 1,
    Decreased = 2,
}

public enum EnergyLevel
{
    High = 0,
    Medium = 1,
    Low = 2,
}

[Flags]
public enum TasteCraving
{
    None = 0,
    Sweet = 1,
    Salty = 2,
    Sour = 4,
}

[Flags]
public enum DigestionState
{
    None = 0,
    Normal = 1,
    Constipation = 2,
    Bloating = 4,
    Indigestion = 8,
}

[Flags]
public enum MenstrualState
{
    None = 0,
    Regular = 1,
    Irregular = 2,
    Painful = 4,
    Menopause = 8,
}

[Flags]
public enum Mood
{
    None = 0,
    Calm = 1,
    Anxious = 2,
    Depressed = 4,
    Aggressive = 8,
    Variable = 16,
}

[Flags]
public enum PulseQuality
{
    None = 0,
    Weak = 1,
    Strong = 2,
    Rapid = 4,
    Slow = 8,
}

[Flags]
public enum TreatmentArea
{
    None = 0,
    Face = 1,
    Abdomen = 2,
    Back = 4,
    Arm = 8,
    Leg = 16,
}

[Flags]
public enum TreatmentMethod
{
    None = 0,
    Needling = 1,
    Embedding = 2,
    Moxibustion = 4,
    Cupping = 8,
    Laser = 16,
    Acupressure = 32,
}

/// <summary>
/// One medical record per patient, written by the doctor. It follows the clinic's intake form: reason for
/// the visit, history, general state, measurements, TCM diagnosis, treatment plan, consent and the doctor's
/// notes. Personal details (section 1 of the form) are in <see cref="PatientProfile"/>; sessions are
/// <see cref="TreatmentSession"/>s.
/// </summary>
public class MedicalRecord
{
    public int Id { get; set; }
    public string PatientUserId { get; set; } = default!;

    // 2. Reason for the visit.
    public string? ChiefComplaint { get; set; }
    public string? ProblemDuration { get; set; }
    public TreatmentGoal Goals { get; set; }
    public string? GoalsNotes { get; set; }

    // 3. Medical history, medications and allergies.
    public MedicalCondition Conditions { get; set; }
    public string? OtherConditions { get; set; }
    public string? Medications { get; set; }
    public string? Surgeries { get; set; }
    public bool? HasDrugAllergy { get; set; }
    public string? DrugAllergies { get; set; }
    public bool? HasOtherAllergy { get; set; }
    public string? OtherAllergies { get; set; }

    // 4. General state.
    public SleepQuality? Sleep { get; set; }
    public AppetiteLevel? Appetite { get; set; }
    public TasteCraving Cravings { get; set; }
    public DigestionState Digestion { get; set; }
    public EnergyLevel? Energy { get; set; }
    public MenstrualState Menstrual { get; set; }
    public DateOnly? LastMenstrualPeriod { get; set; }
    public Mood Mood { get; set; }

    // 5. Measurements.
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public double? WeightAfterKg { get; set; }
    public double? WaistCm { get; set; }
    public double? HipCm { get; set; }
    public double? ThighCm { get; set; }
    public double? ArmCm { get; set; }
    public string? ObservedChanges { get; set; }

    // 6. TCM diagnosis. The medical diagnosis is encrypted at rest.
    public PulseQuality Pulse { get; set; }
    public string? TongueColor { get; set; }
    public string? TongueCoating { get; set; }
    public string? TcmPattern { get; set; }
    public string? Icd10 { get; set; }
    public string? Diagnosis { get; set; }

    // 7. Treatment plan.
    public string? ShortTermGoals { get; set; }
    public string? LongTermGoals { get; set; }
    public TreatmentArea Areas { get; set; }
    public string? OtherAreas { get; set; }
    public TreatmentMethod Methods { get; set; }
    public int? PlannedSessions { get; set; }
    public string? SessionInterval { get; set; }
    public string? AfterCareAdvice { get; set; }

    // 9 and 10. Consent and the doctor's notes.
    public DateTime? ConsentSignedUtc { get; set; }
    public string? DoctorNotes { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public bool HasAllergies => HasDrugAllergy == true || HasOtherAllergy == true || DrugAllergies is not null || OtherAllergies is not null;

    public double? Bmi => HeightCm is > 0 && WeightKg is > 0 ? Math.Round(WeightKg.Value / Math.Pow(HeightCm.Value / 100, 2), 1) : null;

    /// <summary>Basal metabolic rate in kcal/day (Mifflin-St Jeor), from the first-visit weight.</summary>
    public int? Bmr(int? age, Gender? gender)
    {
        if (HeightCm is not > 0 || WeightKg is not > 0 || age is not > 0 || gender is null)
        {
            return null;
        }
        var bmr = 10 * WeightKg.Value + 6.25 * HeightCm.Value - 5 * age.Value + (gender == Gender.Male ? 5 : -161);
        return (int)Math.Round(bmr);
    }
}
