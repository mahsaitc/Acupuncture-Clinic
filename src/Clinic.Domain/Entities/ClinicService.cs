namespace Clinic.Domain.Entities;

/// <summary>A bookable service, e.g. acupuncture, catgut embedding, general visit.</summary>
public class ClinicService
{
    public int Id { get; set; }
    public string NameFa { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public string Name(bool english) => english ? NameEn : NameFa;
}
