namespace Clinic.Domain;

/// <summary>Application roles. Names are stored in the database, so do not rename them.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Doctor = "Doctor";
    public const string Receptionist = "Receptionist";
    public const string Patient = "Patient";

    /// <summary>
    /// The site owner, above the admin: changes the clinic's name, logo and colours. Given only from configuration
    /// (<c>Owner:Email</c>) at start-up, never from a page, so it is deliberately not in <see cref="All"/>.
    /// </summary>
    public const string Owner = "Owner";

    public static readonly string[] All = [Admin, Doctor, Receptionist, Patient];

    /// <summary>Staff who may manage the appointment calendar.</summary>
    public const string Staff = Admin + "," + Doctor + "," + Receptionist;
}
