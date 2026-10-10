using Clinic.Application.Common;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Clinical;

/// <summary>A Medical Council number belongs to one doctor only.</summary>
public static class CouncilNumbers
{
    public static string Normalize(string? value) => JalaliDate.ToLatinDigits(value ?? "").Trim();

    /// <summary>True when another doctor's profile already has this number.</summary>
    public static async Task<bool> InUseAsync(ClinicDbContext db, string? value, string? exceptUserId = null)
    {
        var number = Normalize(value);
        return number.Length > 0
            && await db.Doctors.AnyAsync(d => d.MedicalCouncilNumber == number && d.UserId != exceptUserId);
    }
}
