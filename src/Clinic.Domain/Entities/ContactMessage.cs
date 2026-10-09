namespace Clinic.Domain.Entities;

/// <summary>A message sent from the public contact form to the clinic.</summary>
public class ContactMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;

    /// <summary>Set when the sender was logged in, so staff can open their record.</summary>
    public string? UserId { get; set; }

    /// <summary>The doctor the message is for, or null for the clinic in general.</summary>
    public int? DoctorProfileId { get; set; }

    /// <summary>Set when the message was left with an appointment.</summary>
    public int? AppointmentId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadUtc { get; set; }
    public bool IsArchived { get; set; }
}
