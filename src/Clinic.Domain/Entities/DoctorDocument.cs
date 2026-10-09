namespace Clinic.Domain.Entities;

public enum DoctorDocumentKind
{
    MedicalLicense = 0,
    NationalIdCard = 1,
    OtherDocument = 2,
}

/// <summary>A document a doctor uploads when signing up, for the admin to check before approving the account.</summary>
public class DoctorDocument
{
    public int Id { get; set; }
    public int DoctorProfileId { get; set; }
    public DoctorDocumentKind Kind { get; set; }

    /// <summary>Random name inside the private store.</summary>
    public string StoredName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public DateTime UploadedUtc { get; set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}
