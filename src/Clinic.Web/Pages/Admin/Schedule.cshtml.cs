using Clinic.Web.Clinical;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin;

/// <summary>Each doctor's weekly working hours, which drive the free slots shown to patients. Only the admin sets them.</summary>
[Authorize(Policy = Policies.Admin)]
public class ScheduleModel(ClinicDbContext db, StaffScope scope, IStringLocalizer<SharedResource> l) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    public List<(int Id, string Name)> Doctors { get; private set; } = [];

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
            return RedirectToPage(new { DoctorId });
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
        return RedirectToPage(new { DoctorId });
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
        return RedirectToPage(new { DoctorId });
    }

    /// <summary>The chosen doctor, or the first one (the admin's own profile when they are a doctor).</summary>
    private async Task<DoctorProfile?> LoadProfileAsync()
    {
        Doctors = await scope.DoctorsAsync();
        if (DoctorId is null || Doctors.All(d => d.Id != DoctorId))
        {
            DoctorId = (await scope.DoctorAsync())?.Id is int own && Doctors.Any(d => d.Id == own) ? own : Doctors.FirstOrDefault().Id;
        }
        return await db.Doctors.Include(d => d.WorkingHours).FirstOrDefaultAsync(d => d.Id == DoctorId && d.IsApproved);
    }
}
