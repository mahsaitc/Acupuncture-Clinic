namespace Clinic.Application.Scheduling;

public enum BookingError
{
    None = 0,
    ServiceNotFound,
    DoctorNotAvailable,
    SlotNotAvailable,
    NotAllowed,
}

public sealed record BookingResult(int? AppointmentId, BookingError Error)
{
    public bool Succeeded => Error == BookingError.None;

    public static BookingResult Ok(int id) => new(id, BookingError.None);

    public static BookingResult Fail(BookingError error) => new(null, error);
}

public interface IBookingService
{
    /// <summary>Free slot start times (UTC) for one doctor, service and local date.</summary>
    Task<IReadOnlyList<DateTime>> GetFreeSlotsAsync(int doctorProfileId, int serviceId, DateOnly localDate, CancellationToken ct = default);

    Task<BookingResult> BookAsync(int doctorProfileId, int serviceId, string patientUserId, DateTime startUtc, string? note, CancellationToken ct = default);

    /// <summary>Cancels an appointment. Patients may cancel only their own future appointments.</summary>
    Task<bool> CancelAsync(int appointmentId, string userId, bool isStaff, CancellationToken ct = default);
}
