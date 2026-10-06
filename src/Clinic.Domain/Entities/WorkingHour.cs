namespace Clinic.Domain.Entities;

/// <summary>A weekly recurring block in which a doctor accepts appointments (clinic local time).</summary>
public class WorkingHour
{
    public int Id { get; set; }
    public int DoctorProfileId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}
