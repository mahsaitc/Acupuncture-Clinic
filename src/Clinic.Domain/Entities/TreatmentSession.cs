namespace Clinic.Domain.Entities;

public enum SessionType
{
    Acupuncture = 0,
    CatgutEmbedding = 1,
    Electroacupuncture = 2,
    Cupping = 3,
    AuricularTherapy = 4,
    Consultation = 5,
}

public class TreatmentSession
{
    public int Id { get; set; }
    public string PatientUserId { get; set; } = default!;
    public string DoctorUserId { get; set; } = default!;

    public DateTime DateUtc { get; set; }
    public SessionType Type { get; set; }

    public double? WeightKg { get; set; }
    public double? WaistCm { get; set; }

    /// <summary>Patient-reported pain, 0 to 10.</summary>
    public int? PainScore { get; set; }
    public int? NeedleRetentionMinutes { get; set; }

    public string? Complaint { get; set; }
    public string? Reactions { get; set; }
    public string? Notes { get; set; }
    public string? NextPlan { get; set; }

    public List<SessionPoint> Points { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public enum PointSide
{
    Midline = 0,
    Left = 1,
    Right = 2,
    Both = 3,
}

/// <summary>
/// A point recorded in a session. A standard point is stored by its code and side and is drawn from the
/// point library on every chart it appears on. A point the doctor placed by hand has no code and keeps
/// the chart and position it was placed at.
/// </summary>
public class SessionPoint
{
    public int Id { get; set; }
    public int TreatmentSessionId { get; set; }

    /// <summary>Standard code such as ST36, or null for a point the doctor placed freely.</summary>
    public string? Code { get; set; }
    public string Label { get; set; } = "";
    public PointSide Side { get; set; }

    /// <summary>Chart key (such as "front" or "ear") and position, for hand-placed points only.</summary>
    public string? View { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }

    public string? Note { get; set; }
}
