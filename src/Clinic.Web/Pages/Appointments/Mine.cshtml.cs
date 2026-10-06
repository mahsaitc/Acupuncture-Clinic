using System.Security.Claims;
using Clinic.Application.Scheduling;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Appointments;

public class MineModel(ClinicDbContext db, IBookingService booking, TimeProvider time, IStringLocalizer<SharedResource> l) : PageModel
{
    public List<Appointment> Upcoming { get; private set; } = [];
    public List<Appointment> Past { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = time.GetUtcNow().UtcDateTime;
        var all = await db.Appointments.AsNoTracking()
            .Include(a => a.Service)
            .Where(a => a.PatientUserId == userId)
            .OrderByDescending(a => a.StartUtc)
            .Take(100)
            .ToListAsync();

        Upcoming = all.Where(a => a.StartUtc > now && a.Status != AppointmentStatus.Cancelled).OrderBy(a => a.StartUtc).ToList();
        Past = all.Except(Upcoming).ToList();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        TempData["Message"] = await booking.CancelAsync(id, userId, isStaff: false)
            ? l["The appointment was cancelled."].Value
            : l["This appointment cannot be cancelled."].Value;
        return RedirectToPage();
    }
}
