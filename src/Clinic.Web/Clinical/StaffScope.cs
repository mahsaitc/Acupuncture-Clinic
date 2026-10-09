using System.Security.Claims;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Clinical;

/// <summary>
/// What the signed-in staff member may see. The admin and the receptionist see the whole clinic; a doctor who is not
/// an admin sees only their own appointments, messages and patients.
/// </summary>
public class StaffScope(ClinicDbContext db, UserManager<ApplicationUser> users, IHttpContextAccessor http)
{
    private DoctorProfile? doctor;
    private bool loaded;

    private ClaimsPrincipal User => http.HttpContext?.User ?? new ClaimsPrincipal();

    public string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAdmin => User.IsInRole(Roles.Admin);

    /// <summary>True for a doctor who is not an admin: their pages show only their own work.</summary>
    public bool IsOwnOnly => User.IsInRole(Roles.Doctor) && !IsAdmin;

    /// <summary>The signed-in user's doctor profile, if they have one.</summary>
    public async Task<DoctorProfile?> DoctorAsync()
    {
        if (!loaded)
        {
            var id = UserId;
            doctor = id is null ? null : await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == id);
            loaded = true;
        }
        return doctor;
    }

    /// <summary>The doctor whose work the pages are limited to, or null when the user sees the whole clinic.</summary>
    public async Task<int?> OwnDoctorIdAsync() => IsOwnOnly ? (await DoctorAsync())?.Id ?? -1 : null;

    /// <summary>Limits patient users to the doctor's own patients: assigned to them, booked with them or treated by them.</summary>
    public async Task<IQueryable<ApplicationUser>> PatientsAsync(IQueryable<ApplicationUser> patients)
    {
        if (await OwnDoctorIdAsync() is not int doctorId)
        {
            return patients;
        }
        var userId = UserId;
        return patients.Where(u =>
            db.PatientProfiles.Any(p => p.UserId == u.Id && p.DoctorProfileId == doctorId)
            || db.Appointments.Any(a => a.PatientUserId == u.Id && a.DoctorProfileId == doctorId)
            || db.TreatmentSessions.Any(s => s.PatientUserId == u.Id && s.DoctorUserId == userId));
    }

    /// <summary>Limits appointments to the doctor's own.</summary>
    public async Task<IQueryable<Appointment>> AppointmentsAsync(IQueryable<Appointment> appointments) =>
        await OwnDoctorIdAsync() is int doctorId ? appointments.Where(a => a.DoctorProfileId == doctorId) : appointments;

    /// <summary>
    /// A doctor sees the messages sent to them; the receptionist sees the clinic's general messages; the admin sees all.
    /// </summary>
    public async Task<IQueryable<ContactMessage>> MessagesAsync(IQueryable<ContactMessage> messages)
    {
        if (IsAdmin)
        {
            return messages;
        }
        var doctorId = (await DoctorAsync())?.Id;
        if (User.IsInRole(Roles.Doctor))
        {
            return messages.Where(m => m.DoctorProfileId == doctorId);
        }
        return messages.Where(m => m.DoctorProfileId == null);
    }

    /// <summary>Finds a patient the user may open, or null.</summary>
    public async Task<ApplicationUser?> FindPatientAsync(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        var patients = await PatientsAsync(db.Users.Where(u => u.Id == id));
        var user = await patients.FirstOrDefaultAsync();
        return user is not null && await users.IsInRoleAsync(user, Roles.Patient) ? user : null;
    }

    /// <summary>Approved doctors with their names, for pickers.</summary>
    public async Task<List<(int Id, string Name)>> DoctorsAsync() =>
        (await db.Doctors.AsNoTracking().Where(d => d.IsApproved)
            .Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d.Id, u.FullName })
            .OrderBy(d => d.FullName)
            .ToListAsync())
        .Select(d => (d.Id, d.FullName))
        .ToList();
}
