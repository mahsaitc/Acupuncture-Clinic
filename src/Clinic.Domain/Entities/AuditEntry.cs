namespace Clinic.Domain.Entities;

public enum AuditAction
{
    ViewRecord = 0,
    UpdateRecord = 1,
    CreateSession = 2,
    UpdateSession = 3,
    DeleteSession = 4,
    UploadFile = 5,
    ViewFile = 6,
    DeleteFile = 7,
    UpdateFile = 8,
}

/// <summary>Who touched which patient's clinical data, and when. Entries are only ever added.</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string UserId { get; set; } = default!;
    public AuditAction Action { get; set; }
    public string PatientUserId { get; set; } = default!;
    public string? EntityId { get; set; }
    public string? Ip { get; set; }
}
