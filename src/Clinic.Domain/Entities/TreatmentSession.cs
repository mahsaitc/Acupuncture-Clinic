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

public enum BodyView
{
    Front = 0,
    Back = 1,
}

public enum PointSide
{
    Midline = 0,
    Left = 1,
    Right = 2,
}

/// <summary>A point marked on the body diagram. X and Y are in diagram units (200 x 480).</summary>
public class SessionPoint
{
    public int Id { get; set; }
    public int TreatmentSessionId { get; set; }

    /// <summary>Standard code such as ST36, or null for a point the doctor placed freely.</summary>
    public string? Code { get; set; }
    public string Label { get; set; } = "";
    public BodyView View { get; set; }
    public PointSide Side { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string? Note { get; set; }
}
