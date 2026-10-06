using Clinic.Application.Common;
using Clinic.Application.Scheduling;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Scheduling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Clinic.Tests;

public sealed class BookingServiceTests : IDisposable
{
    private static readonly ClinicTime Tehran = new("Asia/Tehran");
    private static readonly DateOnly Saturday = new(2026, 10, 10);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ClinicDbContext _db;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero));
    private readonly BookingService _booking;
    private readonly int _doctorId;
    private readonly int _serviceId;

    public BookingServiceTests()
    {
        _connection.Open();
        _db = new ClinicDbContext(new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.AddRange(
            new ApplicationUser { Id = "doc", UserName = "doc@x", FullName = "Dr" },
            new ApplicationUser { Id = "p1", UserName = "p1@x", FullName = "P1" },
            new ApplicationUser { Id = "p2", UserName = "p2@x", FullName = "P2" });
        var doctor = new DoctorProfile
        {
            UserId = "doc",
            MedicalCouncilNumber = "12345",
            IsApproved = true,
            WorkingHours = [new WorkingHour { DayOfWeek = DayOfWeek.Saturday, Start = new(9, 0), End = new(12, 0) }],
        };
        var service = new ClinicService { NameFa = "طب سوزنی", NameEn = "Acupuncture", DurationMinutes = 60 };
        _db.AddRange(doctor, service);
        _db.SaveChanges();
        _doctorId = doctor.Id;
        _serviceId = service.Id;

        _booking = new BookingService(_db, Tehran, _time);
    }

    [Fact]
    public async Task Lists_free_slots_for_the_working_day()
    {
        var slots = await _booking.GetFreeSlotsAsync(_doctorId, _serviceId, Saturday);

        Assert.Equal(3, slots.Count);
        Assert.Empty(await _booking.GetFreeSlotsAsync(_doctorId, _serviceId, Saturday.AddDays(1)));
    }

    [Fact]
    public async Task The_same_slot_cannot_be_booked_twice()
    {
        var slot = Tehran.ToUtc(Saturday, new(10, 0));

        var first = await _booking.BookAsync(_doctorId, _serviceId, "p1", slot, null);
        var second = await _booking.BookAsync(_doctorId, _serviceId, "p2", slot, null);

        Assert.True(first.Succeeded);
        Assert.Equal(BookingError.SlotNotAvailable, second.Error);
        Assert.DoesNotContain(slot, await _booking.GetFreeSlotsAsync(_doctorId, _serviceId, Saturday));
    }

    [Fact]
    public async Task A_time_outside_working_hours_is_rejected()
    {
        var result = await _booking.BookAsync(_doctorId, _serviceId, "p1", Tehran.ToUtc(Saturday, new(15, 0)), null);

        Assert.Equal(BookingError.SlotNotAvailable, result.Error);
    }

    [Fact]
    public async Task An_unapproved_doctor_cannot_be_booked()
    {
        var doctor = await _db.Doctors.SingleAsync();
        doctor.IsApproved = false;
        await _db.SaveChangesAsync();

        var result = await _booking.BookAsync(_doctorId, _serviceId, "p1", Tehran.ToUtc(Saturday, new(9, 0)), null);

        Assert.Equal(BookingError.DoctorNotAvailable, result.Error);
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_again()
    {
        var slot = Tehran.ToUtc(Saturday, new(9, 0));
        var booked = await _booking.BookAsync(_doctorId, _serviceId, "p1", slot, null);

        Assert.True(await _booking.CancelAsync(booked.AppointmentId!.Value, "p1", isStaff: false));
        Assert.Contains(slot, await _booking.GetFreeSlotsAsync(_doctorId, _serviceId, Saturday));
    }

    [Fact]
    public async Task A_patient_cannot_cancel_someone_elses_appointment()
    {
        var booked = await _booking.BookAsync(_doctorId, _serviceId, "p1", Tehran.ToUtc(Saturday, new(9, 0)), null);

        Assert.False(await _booking.CancelAsync(booked.AppointmentId!.Value, "p2", isStaff: false));
        Assert.True(await _booking.CancelAsync(booked.AppointmentId!.Value, "p2", isStaff: true));
    }

    [Fact]
    public async Task A_patient_cannot_cancel_after_the_appointment_started()
    {
        var slot = Tehran.ToUtc(Saturday, new(9, 0));
        var booked = await _booking.BookAsync(_doctorId, _serviceId, "p1", slot, null);
        _time.SetUtcNow(slot.AddMinutes(5));

        Assert.False(await _booking.CancelAsync(booked.AppointmentId!.Value, "p1", isStaff: false));
        Assert.Equal(AppointmentStatus.Booked, (await _db.Appointments.AsNoTracking().SingleAsync()).Status);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
