using System.Security.Claims;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

/// <summary>A doctor's weekly working hours, which drive the free slots shown to patients.</summary>
[Authorize(Policy = Policies.Doctor)]
public class ScheduleModel(ClinicDbContext db, IStringLocalizer<SharedResource> l) : PageModel
{
    /// <summary>Iranian week order, Saturday first.</summary>
    public static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
        DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    ];

    public DoctorProfile? Profile { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Profile = await LoadProfileAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        var profile = await LoadProfileAsync();
        if (profile is null)
        {
            return RedirectToPage();
        }

        if (end <= start)
        {
            TempData["Message"] = l["The end time must be after the start time."].Value;
        }
        else if (profile.WorkingHours.Any(w => w.DayOfWeek == day && w.Start < end && start < w.End))
        {
            TempData["Message"] = l["This block overlaps an existing block."].Value;
        }
        else
        {
            profile.WorkingHours.Add(new WorkingHour { DayOfWeek = day, Start = start, End = end });
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id)
    {
        var profile = await LoadProfileAsync();
        var block = profile?.WorkingHours.FirstOrDefault(w => w.Id == id);
        if (block is not null)
        {
            db.WorkingHours.Remove(block);
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    private Task<DoctorProfile?> LoadProfileAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return db.Doctors.Include(d => d.WorkingHours).FirstOrDefaultAsync(d => d.UserId == userId);
    }
}
