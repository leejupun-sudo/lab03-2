using CMS.API.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline (routing, model binding, validation, JSON) with the
/// SQL-backed repository swapped for <see cref="FakePublishStatusRepository"/>, so the
/// endpoint tests need no database.
/// </summary>
public class PublishStatusApiFactory : ApiFactory
{
    // Mirrors the three rows in the live CMS database. pkid 2 carries usage so the
    // delete guard has something to block on; pkid 1 and 3 are free to delete.
    public FakePublishStatusRepository Repository { get; } = new FakePublishStatusRepository()
        .Seed(1, "草稿", isDraft: true, isPublished: false, isDiscontinued: false)
        .Seed(2, "上架中", isDraft: false, isPublished: true, isDiscontinued: false, courseCount: 12, promotion2Count: 3)
        .Seed(3, "已下架", isDraft: false, isPublished: false, isDiscontinued: true);

    protected override void ConfigureFakes(IServiceCollection services)
    {
        services.RemoveAll<IPublishStatusRepository>();
        services.AddSingleton<IPublishStatusRepository>(Repository);
    }
}
