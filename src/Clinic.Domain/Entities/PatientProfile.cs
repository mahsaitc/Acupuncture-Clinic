namespace Clinic.Domain.Entities;

public enum MaritalStatus
{
    Single = 0,
    Married = 1,
    Other = 2,
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
    public string? FatherName { get; set; }
    public string? Occupation { get; set; }
    public string? Education { get; set; }

    public string? LandlinePhone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }

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
