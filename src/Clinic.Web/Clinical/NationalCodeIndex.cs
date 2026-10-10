using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Clinic.Application.Common;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Clinical;

/// <summary>
/// Keeps national codes unique across every account. The codes are stored encrypted, which cannot be compared,
/// so each account also keeps a keyed hash (HMAC) of its code. The key lives next to the Data Protection keys and
/// must be backed up with them.
/// </summary>
public sealed class NationalCodeIndex(ClinicDbContext db, IConfiguration config, IWebHostEnvironment env, ILogger<NationalCodeIndex> logger)
{
    private static readonly ConcurrentDictionary<string, byte[]> Keys = new();

    /// <summary>Latin digits only, or null when nothing was entered.</summary>
    public static string? Normalize(string? code)
    {
        var digits = new string(JalaliDate.ToLatinDigits(code ?? "").Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    public string? Hash(string? code) =>
        Normalize(code) is { } digits ? Convert.ToHexString(HMACSHA256.HashData(Key(), Encoding.ASCII.GetBytes(digits))) : null;

    /// <summary>True when another account already has this national code.</summary>
    public async Task<bool> InUseAsync(string? code, string? exceptUserId)
    {
        var hash = Hash(code);
        return hash is not null && await db.Users.AnyAsync(u => u.NationalCodeHash == hash && u.Id != exceptUserId);
    }

    /// <summary>Gives accounts created before the check existed their hash. A duplicate is left out and logged.</summary>
    public async Task BackfillAsync()
    {
        var missing = await db.Users.Where(u => u.NationalCodeHash == null).Select(u => u.Id).ToListAsync();
        if (missing.Count == 0)
        {
            return;
        }
        var codes = (await db.PatientProfiles.AsNoTracking().Where(p => missing.Contains(p.UserId) && p.NationalCode != null)
                .Select(p => new { p.UserId, p.NationalCode }).ToListAsync())
            .Concat(await db.Doctors.AsNoTracking().Where(d => missing.Contains(d.UserId) && d.NationalCode != null)
                .Select(d => new { d.UserId, d.NationalCode }).ToListAsync());

        var taken = (await db.Users.Where(u => u.NationalCodeHash != null).Select(u => u.NationalCodeHash!).ToListAsync()).ToHashSet();
        foreach (var c in codes)
        {
            if (Hash(c.NationalCode) is not { } hash)
            {
                continue;
            }
            if (!taken.Add(hash))
            {
                logger.LogWarning("Account {UserId} has a national code that another account already uses; it was left without the uniqueness hash.", c.UserId);
                continue;
            }
            var user = await db.Users.FindAsync(c.UserId);
            if (user is not null && user.NationalCodeHash is null)
            {
                user.NationalCodeHash = hash;
            }
        }
        await db.SaveChangesAsync();
    }

    private byte[] Key()
    {
        var folder = Path.GetFullPath(config["DataProtection:KeysPath"] ?? Path.Combine(env.ContentRootPath, "keys"));
        return Keys.GetOrAdd(folder, f =>
        {
            var path = Path.Combine(f, "national-code.key");
            if (File.Exists(path))
            {
                return Convert.FromBase64String(File.ReadAllText(path).Trim());
            }
            Directory.CreateDirectory(f);
            var key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllText(path, Convert.ToBase64String(key));
            return key;
        });
    }
}
