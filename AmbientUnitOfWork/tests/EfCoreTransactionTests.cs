using AmbientUnitOfWork.Tests.Infrastructure;
using AmbientUnitOfWork.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AmbientUnitOfWork.Tests;

[Collection("SqlServer")]
public sealed class EfCoreTransactionTests(SqlServerFixture fixture)
{
    private readonly IDbContextFactory<DemoDbContext> factory = fixture.Factory;

    [Fact]
    public async Task Commit_PersistsBothEntities()
    {
        // Arrange
        var product = new Product { Name = $"ef-commit-{Guid.NewGuid():N}" };
        var customer = new Customer { Email = $"ef-commit-{Guid.NewGuid():N}@example.test" };

        // Act
        await using (var context = await factory.CreateDbContextAsync())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Products.Add(product);
            await context.SaveChangesAsync();
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.NotNull(await verification.Products.FindAsync(product.Id));
        Assert.NotNull(await verification.Customers.FindAsync(customer.Id));
    }

    [Fact]
    public async Task DisposeWithoutCommit_RollsBackSavedChanges()
    {
        // Arrange
        var product = new Product { Name = $"ef-rollback-{Guid.NewGuid():N}" };
        var customer = new Customer { Email = $"ef-rollback-{Guid.NewGuid():N}@example.test" };

        // Act
        await using (var context = await factory.CreateDbContextAsync())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Products.Add(product);
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            Assert.NotNull(await context.Products.FindAsync(product.Id));
            Assert.NotNull(await context.Customers.FindAsync(customer.Id));
        }

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Null(await verification.Products.FindAsync(product.Id));
        Assert.Null(await verification.Customers.FindAsync(customer.Id));
    }
}
