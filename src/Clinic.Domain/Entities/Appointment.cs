namespace Clinic.Domain.Entities;

public enum AppointmentStatus
{
    Booked = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4,
}

public class Appointment
{
    public int Id { get; set; }
    public int DoctorProfileId { get; set; }
    public DoctorProfile? Doctor { get; set; }
    public string PatientUserId { get; set; } = default!;
    public int ClinicServiceId { get; set; }
    public ClinicService? Service { get; set; }

    /// <summary>Start time in UTC. Convert to clinic time only for display.</summary>
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public string? PatientNote { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public bool BlocksTime => Status is not (AppointmentStatus.Cancelled);

    public bool Overlaps(DateTime startUtc, DateTime endUtc) => StartUtc < endUtc && startUtc < EndUtc;
}
