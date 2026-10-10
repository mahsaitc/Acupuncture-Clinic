using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Pages.Admin;

/// <summary>Read-only list of who opened or changed patients' clinical data.</summary>
[Authorize(Policy = Policies.Admin)]
public class AuditModel(ClinicDbContext db) : PageModel
{
    private const int PageSize = 50;
    private const string PageQueryKey = "p";

    [BindProperty(SupportsGet = true)]
    public string? PatientId { get; set; }

    [BindProperty(SupportsGet = true, Name = PageQueryKey)]
    public int PageNumber { get; set; } = 1;

    public int PageCount { get; private set; }
    public string? PatientName { get; private set; }
    public List<AuditEntry> Entries { get; private set; } = [];
    public Dictionary<string, string> Names { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrEmpty(PatientId))
        {
            query = query.Where(a => a.PatientUserId == PatientId);
            PatientName = await db.Users.Where(u => u.Id == PatientId).Select(u => u.FullName).FirstOrDefaultAsync();
        }

        var total = await query.CountAsync();
        PageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, PageCount);
        Entries = await query.OrderByDescending(a => a.Id).Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync();

        var ids = Entries.SelectMany(a => new[] { a.UserId, a.PatientUserId }).Distinct().ToList();
        Names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }
}
