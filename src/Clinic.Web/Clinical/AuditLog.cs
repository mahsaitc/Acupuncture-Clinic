using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;

namespace Clinic.Web.Clinical;

/// <summary>Records who read or changed a patient's clinical data.</summary>
public class AuditLog(ClinicDbContext db, IHttpContextAccessor http, TimeProvider time)
{
    /// <summary>Adds an entry to the context. It is written by the caller's next SaveChanges.</summary>
    public void Add(AuditAction action, string patientUserId, object? entityId = null)
    {
        var context = http.HttpContext;
        db.AuditEntries.Add(new AuditEntry
        {
            TimestampUtc = time.GetUtcNow().UtcDateTime,
            UserId = context?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system",
            Action = action,
            PatientUserId = patientUserId,
            EntityId = entityId?.ToString(),
            Ip = context?.Connection.RemoteIpAddress?.ToString(),
        });
    }

    /// <summary>Adds an entry and saves it at once, for reads that change nothing else.</summary>
    public async Task WriteAsync(AuditAction action, string patientUserId, object? entityId = null)
    {
        Add(action, patientUserId, entityId);
        await db.SaveChangesAsync();
    }
}
