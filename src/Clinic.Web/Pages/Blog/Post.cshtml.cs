using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Content;

namespace Clinic.Web.Pages.Blog;

public class PostModel(ClinicDbContext db) : PostPageModel(db)
{
    public override PostKind Kind => PostKind.Blog;
}
