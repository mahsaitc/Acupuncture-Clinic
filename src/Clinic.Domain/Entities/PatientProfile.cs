namespace Clinic.Domain.Entities;

public enum MaritalStatus
{
    Single = 0,
    Married = 1,
    Other = 2,
}

/// <summary>How the patient heard of the clinic, kept as a fixed list so it can be counted.</summary>
public enum ReferralChannel
{
    Friend = 0,
    Instagram = 1,
    Internet = 2,
    Website = 3,
    Doctor = 4,
    Other = 5,
}

/// <summary>
/// A patient's non-clinical details, which the receptionist may enter and edit.
/// Clinical data lives in <see cref="MedicalRecord"/>, which only doctors can open.
/// </summary>
public class PatientProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;

    /// <summary>Encrypted at rest.</summary>
    public string? NationalCode { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public int? Children { get; set; }
    public string? FatherName { get; set; }
    public string? Occupation { get; set; }
    public string? Education { get; set; }

    public string? LandlinePhone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>The first visit to the clinic ("date of visit" on the intake form).</summary>
    public DateOnly? FirstVisitDate { get; set; }

    public ReferralChannel? Referral { get; set; }

    /// <summary>Optional detail, e.g. the friend's or the referring doctor's name.</summary>
    public string? ReferralSource { get; set; }
    public string? InsuranceProvider { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Front-desk notes, such as preferred visiting times. Nothing medical.</summary>
    public string? Notes { get; set; }

    public string? CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public int? AgeOn(DateOnly today)
    {
        if (BirthDate is not DateOnly b)
        {
            return null;
        }
        var age = today.Year - b.Year;
        return b > today.AddYears(-age) ? age - 1 : age;
    }
}
