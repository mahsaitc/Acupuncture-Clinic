using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Clinic.Web.Clinical;

/// <summary>
/// Returns 404 when a page is asked for a patient the signed-in doctor may not open (see <see cref="StaffScope"/>).
/// Applied to whole folders so that no handler can forget the check.
/// </summary>
public sealed class PatientAccessFilter(string parameter) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var id = context.HandlerArguments.TryGetValue(parameter, out var value) ? value as string : null;
        id ??= request.Query[parameter].FirstOrDefault()
            ?? (request.HasFormContentType ? request.Form[parameter].FirstOrDefault() : null);

        if (!string.IsNullOrEmpty(id))
        {
            var scope = context.HttpContext.RequestServices.GetRequiredService<StaffScope>();
            if (scope.IsOwnOnly && await scope.FindPatientAsync(id) is null)
            {
                context.Result = new NotFoundResult();
                return;
            }
        }
        await next();
    }
}
