namespace Clinic.Domain.Entities;

public enum FileCategory
{
    Radiology = 0,
    Lab = 1,
    Photo = 2,
    Other = 3,
}

/// <summary>A patient's file (radiology, lab result, photo). Stored privately, never under /media.</summary>
public class MedicalFile
{
    public int Id { get; set; }
    public string PatientUserId { get; set; } = default!;
    public FileCategory Category { get; set; }
    public string Title { get; set; } = "";
    public string? Note { get; set; }

    /// <summary>Random name inside the private store.</summary>
    public string StoredName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }

    public DateOnly? TakenOn { get; set; }
    public string UploadedByUserId { get; set; } = default!;
    public DateTime UploadedUtc { get; set; }

    /// <summary>Whether the patient sees this file on their own page. Files the patient uploads are always visible to them.</summary>
    public bool VisibleToPatient { get; set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}
