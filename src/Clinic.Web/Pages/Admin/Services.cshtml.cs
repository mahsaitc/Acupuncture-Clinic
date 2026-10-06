using System.ComponentModel.DataAnnotations;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

public class ServicesModel(ClinicDbContext db) : PageModel
{
    public List<ClinicService> Services { get; private set; } = [];

    [BindProperty]
    public ServiceInput Input { get; set; } = new();

    public class ServiceInput
    {
        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(100)]
        [Display(Name = "Name (Persian)")]
        public string NameFa { get; set; } = "";

        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(100)]
        [Display(Name = "Name (English)")]
        public string NameEn { get; set; } = "";

        [Range(5, 240, ErrorMessage = "{0} must be between {1} and {2}.")]
        [Display(Name = "Duration (minutes)")]
        public int DurationMinutes { get; set; } = 30;
    }

    public async Task OnGetAsync() => Services = await db.Services.AsNoTracking().OrderBy(s => s.Id).ToListAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        db.Services.Add(new ClinicService
        {
            NameFa = Input.NameFa.Trim(),
            NameEn = Input.NameEn.Trim(),
            DurationMinutes = Input.DurationMinutes,
        });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var service = await db.Services.FindAsync(id);
        if (service is not null)
        {
            service.IsActive = !service.IsActive;
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
