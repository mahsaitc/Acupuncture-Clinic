namespace Clinic.Domain;

/// <summary>Application roles. Names are stored in the database, so do not rename them.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Doctor = "Doctor";
    public const string Receptionist = "Receptionist";
    public const string Patient = "Patient";

    public static readonly string[] All = [Admin, Doctor, Receptionist, Patient];

    /// <summary>Staff who may manage the appointment calendar.</summary>
    public const string Staff = Admin + "," + Doctor + "," + Receptionist;
}
