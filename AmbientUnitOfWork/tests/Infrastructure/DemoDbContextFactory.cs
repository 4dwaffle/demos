using Microsoft.EntityFrameworkCore;

namespace AmbientUnitOfWork.Tests.Infrastructure;

internal sealed class DemoDbContextFactory(string connectionString) : IDbContextFactory<DemoDbContext>
{
    private readonly DbContextOptions<DemoDbContext> options = new DbContextOptionsBuilder<DemoDbContext>()
        .UseSqlServer(connectionString)
        .Options;

    public DemoDbContext CreateDbContext() => new(options);
}