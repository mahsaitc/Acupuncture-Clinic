using System.Data;
using Clinic.Application.Common;
using Clinic.Application.Scheduling;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Scheduling;

public class BookingService(ClinicDbContext db, ClinicTime clinicTime, TimeProvider time) : IBookingService
{
    public async Task<IReadOnlyList<DateTime>> GetFreeSlotsAsync(int doctorProfileId, int serviceId, DateOnly localDate, CancellationToken ct = default)
    {
        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, ct);
        var doctor = await db.Doctors.AsNoTracking().Include(d => d.WorkingHours)
            .FirstOrDefaultAsync(d => d.Id == doctorProfileId && d.IsApproved, ct);
        if (service is null || doctor is null)
        {
            return [];
        }

        var hours = doctor.WorkingHours
            .Where(w => w.DayOfWeek == localDate.DayOfWeek)
            .Select(w => new TimeRange(w.Start, w.End));

        var dayStartUtc = clinicTime.ToUtc(localDate, TimeOnly.MinValue);
        var dayEndUtc = clinicTime.ToUtc(localDate.AddDays(1), TimeOnly.MinValue);
        var busy = await BusyAsync(doctorProfileId, dayStartUtc, dayEndUtc, ct);

        return SlotCalculator.FreeSlots(localDate, hours, service.DurationMinutes, busy, clinicTime.ToUtc, time.GetUtcNow().UtcDateTime);
    }

    public async Task<BookingResult> BookAsync(int doctorProfileId, int serviceId, string patientUserId, DateTime startUtc, string? note, CancellationToken ct = default)
    {
        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, ct);
        if (service is null)
        {
            return BookingResult.Fail(BookingError.ServiceNotFound);
        }

        var localDate = clinicTime.Today(startUtc);

        // Serializable so two people cannot take the same slot at the same moment.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var free = await GetFreeSlotsAsync(doctorProfileId, serviceId, localDate, ct);
        if (free.Count == 0 && !await db.Doctors.AnyAsync(d => d.Id == doctorProfileId && d.IsApproved, ct))
        {
            return BookingResult.Fail(BookingError.DoctorNotAvailable);
        }
        if (!free.Contains(startUtc))
        {
            return BookingResult.Fail(BookingError.SlotNotAvailable);
        }

        var appointment = new Appointment
        {
            DoctorProfileId = doctorProfileId,
            ClinicServiceId = serviceId,
            PatientUserId = patientUserId,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(service.DurationMinutes),
            PatientNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedUtc = time.GetUtcNow().UtcDateTime,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return BookingResult.Ok(appointment.Id);
    }

    public async Task<bool> CancelAsync(int appointmentId, string userId, bool isStaff, CancellationToken ct = default)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null || appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed)
        {
            return false;
        }
        if (!isStaff && (appointment.PatientUserId != userId || appointment.StartUtc <= time.GetUtcNow().UtcDateTime))
        {
            return false;
        }

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<List<BusyRange>> BusyAsync(int doctorProfileId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await db.Appointments.AsNoTracking()
            .Where(a => a.DoctorProfileId == doctorProfileId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.StartUtc < toUtc && fromUtc < a.EndUtc)
            .Select(a => new BusyRange(a.StartUtc, a.EndUtc))
            .ToListAsync(ct);
}
