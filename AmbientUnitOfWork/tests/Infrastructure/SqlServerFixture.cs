using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace AmbientUnitOfWork.Tests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Strong_password_123!")
        .Build();

    public IDbContextFactory<DemoDbContext> Factory => new DemoDbContextFactory(container.GetConnectionString());

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var context = await Factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();
}

[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
