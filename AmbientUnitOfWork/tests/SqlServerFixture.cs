using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace AmbientUnitOfWork.Tests;

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

public sealed class DemoDbContext(DbContextOptions<DemoDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().Property(product => product.Name).HasMaxLength(200);
        modelBuilder.Entity<Customer>().Property(customer => customer.Email).HasMaxLength(256);
    }
}

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
}

internal sealed class DemoDbContextFactory(string connectionString) : IDbContextFactory<DemoDbContext>
{
    private readonly DbContextOptions<DemoDbContext> options = new DbContextOptionsBuilder<DemoDbContext>()
        .UseSqlServer(connectionString)
        .Options;

    public DemoDbContext CreateDbContext() => new(options);
}
