using System.Globalization;
using System.Security.Claims;
using Clinic.Application.Common;
using Clinic.Application.Scheduling;
using Clinic.Domain;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Booking;

public class IndexModel(
    ClinicDbContext db,
    IBookingService booking,
    ClinicTime clinicTime,
    TimeProvider time,
    UserManager<ApplicationUser> userManager,
    DisplayFormat Fmt,
    IStringLocalizer<SharedResource> l) : PageModel
{
    /// <summary>How many days ahead patients may book.</summary>
    public const int DaysAhead = 30;

    [BindProperty(SupportsGet = true)]
    public int? ServiceId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    public DateOnly? SelectedDate { get; private set; }
    public List<SelectListItem> ServiceOptions { get; private set; } = [];
    public List<SelectListItem> DoctorOptions { get; private set; } = [];
    public List<DateOnly> Days { get; private set; } = [];
    public IReadOnlyList<DateTime> Slots { get; private set; } = [];

    public bool IsStaff => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Doctor) || User.IsInRole(Roles.Receptionist);

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync(long startTicks, string? note, string? patientEmail)
    {
        await LoadAsync();
        if (ServiceId is null || DoctorId is null)
        {
            return Page();
        }

        var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (IsStaff && !string.IsNullOrWhiteSpace(patientEmail))
        {
            // Staff book for a patient by email or by mobile (patients registered at the front desk often have no email).
            var key = patientEmail.Trim();
            ApplicationUser? patient;
            if (key.Contains('@'))
            {
                patient = await userManager.FindByEmailAsync(key);
            }
            else
            {
                var mobile = Clinic.Web.Pages.Admin.Patients.PatientRegistration.NormalizeMobile(key);
                patient = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == mobile);
            }
            if (patient is null || !await userManager.IsInRoleAsync(patient, Roles.Patient))
            {
                ModelState.AddModelError(string.Empty, l["No patient with this mobile or email was found."]);
                return Page();
            }
            patientId = patient.Id;
        }

        var startUtc = new DateTime(startTicks, DateTimeKind.Utc);
        var result = await booking.BookAsync(DoctorId.Value, ServiceId.Value, patientId, startUtc, note);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, l["This time is no longer available. Please choose another time."]);
            return Page();
        }

        // A note left with the booking also reaches the doctor's inbox, where it can be read and archived.
        if (!string.IsNullOrWhiteSpace(note) && await userManager.FindByIdAsync(patientId) is { } patientUser)
        {
            db.ContactMessages.Add(new Clinic.Domain.Entities.ContactMessage
            {
                Name = patientUser.FullName,
                Phone = patientUser.PhoneNumber ?? "-",
                Email = patientUser.Email,
                Subject = l["Note with the appointment on {0}", Fmt.DateTime(startUtc)],
                Body = note.Trim(),
                UserId = patientUser.Id,
                DoctorProfileId = DoctorId,
                AppointmentId = result.AppointmentId,
                CreatedUtc = time.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
        }

        TempData["Message"] = l["Your appointment was booked."].Value;
        return RedirectToPage("/Appointments/Mine");
    }

    private async Task LoadAsync()
    {
        var english = CulturePath.IsEnglish;

        var services = await db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id).ToListAsync();
        ServiceOptions = services
            .Select(s => new SelectListItem(s.Name(english), s.Id.ToString(CultureInfo.InvariantCulture), s.Id == ServiceId))
            .ToList();

        var doctors = await db.Doctors.AsNoTracking().Where(d => d.IsApproved)
            .Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d.Id, u.FullName, d.SpecialtyFa, d.SpecialtyEn })
            .OrderBy(d => d.FullName)
            .ToListAsync();
        // With a single doctor there is nothing to choose.
        if (DoctorId is null && doctors.Count == 1)
        {
            DoctorId = doctors[0].Id;
        }
        DoctorOptions = doctors
            .Select(d => new SelectListItem(
                (english ? d.SpecialtyEn : d.SpecialtyFa) is { Length: > 0 } specialty ? $"{d.FullName} ({specialty})" : d.FullName,
                d.Id.ToString(CultureInfo.InvariantCulture),
                d.Id == DoctorId))
            .ToList();

        var today = clinicTime.Today(time.GetUtcNow().UtcDateTime);
        Days = Enumerable.Range(0, DaysAhead).Select(today.AddDays).ToList();

        if (DateOnly.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date >= today && date <= Days[^1])
        {
            SelectedDate = date;
        }

        if (ServiceId is int serviceId && DoctorId is int doctorId && SelectedDate is DateOnly selected)
        {
            Slots = await booking.GetFreeSlotsAsync(doctorId, serviceId, selected);
        }
    }
}
