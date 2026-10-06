namespace Clinic.Web;

public static class Policies
{
    public const string Admin = "Admin";
    public const string Doctor = "Doctor";

    /// <summary>Who may write the blog and medical articles.</summary>
    public const string Content = "Content";

    /// <summary>Everyone who may open the management panel.</summary>
    public const string Staff = "Staff";
}
