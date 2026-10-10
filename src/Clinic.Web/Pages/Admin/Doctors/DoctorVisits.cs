using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin.Doctors;

/// <summary>When a doctor last saw a patient: a past appointment that took place, or a treatment session.</summary>
public static class DoctorVisits
{
    public sealed record LastVisit(DateTime Utc, string PatientUserId, string PatientName);

    private static readonly AppointmentStatus[] NotHeld = [AppointmentStatus.Cancelled, AppointmentStatus.NoShow];

    public static async Task<LastVisit?> LastAsync(ClinicDbContext db, int doctorId, string doctorUserId, DateTime nowUtc, string? patientUserId = null)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Where(a => a.DoctorProfileId == doctorId && a.StartUtc <= nowUtc && !NotHeld.Contains(a.Status))
            .Where(a => patientUserId == null || a.PatientUserId == patientUserId)
            .OrderByDescending(a => a.StartUtc)
            .Select(a => new { Utc = a.StartUtc, a.PatientUserId })
            .FirstOrDefaultAsync();
        var session = await db.TreatmentSessions.AsNoTracking()
            .Where(s => s.DoctorUserId == doctorUserId && s.DateUtc <= nowUtc)
            .Where(s => patientUserId == null || s.PatientUserId == patientUserId)
            .OrderByDescending(s => s.DateUtc)
            .Select(s => new { Utc = s.DateUtc, s.PatientUserId })
            .FirstOrDefaultAsync();
        var last = session is null || (appointment is not null && appointment.Utc > session.Utc) ? appointment : session;
        if (last is null)
        {
            return null;
        }
        var name = await db.Users.Where(u => u.Id == last.PatientUserId).Select(u => u.FullName).FirstOrDefaultAsync();
        return new LastVisit(last.Utc, last.PatientUserId, name ?? "-");
    }
}
