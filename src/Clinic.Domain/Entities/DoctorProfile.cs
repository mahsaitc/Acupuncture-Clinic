namespace Clinic.Domain.Entities;

public class DoctorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;

    /// <summary>Medical Council registration number (شماره نظام پزشکی).</summary>
    public string MedicalCouncilNumber { get; set; } = default!;
    public string? SpecialtyFa { get; set; }
    public string? SpecialtyEn { get; set; }
    public string? BioFa { get; set; }
    public string? BioEn { get; set; }

    /// <summary>Stored encrypted. Kept unique across all accounts through ApplicationUser.NationalCodeHash.</summary>
    public string? NationalCode { get; set; }

    public DateTime? RequestedUtc { get; set; }

    public List<DoctorDocument> Documents { get; set; } = [];

    /// <summary>Doctors self-register but cannot work until an admin approves them.</summary>
    public bool IsApproved { get; set; }

    public List<WorkingHour> WorkingHours { get; set; } = [];
}
