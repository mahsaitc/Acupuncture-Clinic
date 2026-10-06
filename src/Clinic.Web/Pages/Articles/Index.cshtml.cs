using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Web.Content;

namespace Clinic.Web.Pages.Articles;

public class IndexModel(ClinicDbContext db) : PostListPageModel(db)
{
    public override PostKind Kind => PostKind.Article;
}
