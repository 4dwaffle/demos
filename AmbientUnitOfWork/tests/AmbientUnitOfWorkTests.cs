using AmbientUnitOfWork.Tests.Infrastructure;
using AmbientUnitOfWork.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AmbientUnitOfWork.Tests;

[Collection("SqlServer")]
public sealed class AmbientUnitOfWorkTests(SqlServerFixture fixture)
{
    private readonly IDbContextFactory<DemoDbContext> factory = fixture.Factory;

    [Fact]
    public async Task Commit_PersistsWritesFromTwoRepositories()
    {
        // Arrange
        var products = new Repository<Product, DemoDbContext>(factory);
        var customers = new Repository<Customer, DemoDbContext>(factory);
        var product = new Product { Name = $"product-{Guid.NewGuid():N}" };
        var customer = new Customer { Email = $"customer-{Guid.NewGuid():N}@example.test" };

        // Act
        await using (var uow = new UnitOfWorkProvider<DemoDbContext>())
        {
            await products.AddAsync(product);
            await customers.AddAsync(customer);
            Assert.NotNull(await products.GetByIdAsync(product.Id));
            Assert.NotNull(await customers.GetByIdAsync(customer.Id));
            await uow.CommitAsync();
        }

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.NotNull(await verification.Products.FindAsync(product.Id));
        Assert.NotNull(await verification.Customers.FindAsync(customer.Id));
    }

    [Fact]
    public async Task DisposingWithoutCommit_RollsBackSavedChanges()
    {
        // Arrange
        var products = new Repository<Product, DemoDbContext>(factory);
        var product = new Product { Name = $"rollback-{Guid.NewGuid():N}" };

        // Act
        await using (var uow = new UnitOfWorkProvider<DemoDbContext>())
        {
            await products.AddAsync(product);
            Assert.NotNull(await products.GetByIdAsync(product.Id));
        }

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Null(await verification.Products.FindAsync(product.Id));
    }

    [Fact]
    public async Task ExceptionBeforeCommit_RollsBackBothRepositories()
    {
        // Arrange
        var products = new Repository<Product, DemoDbContext>(factory);
        var customers = new Repository<Customer, DemoDbContext>(factory);
        var product = new Product { Name = $"failed-{Guid.NewGuid():N}" };
        var customer = new Customer { Email = $"failed-{Guid.NewGuid():N}@example.test" };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var uow = new UnitOfWorkProvider<DemoDbContext>();
            await products.AddAsync(product);
            await customers.AddAsync(customer);
            throw new InvalidOperationException("Simulated failure");
        });

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Null(await verification.Products.FindAsync(product.Id));
        Assert.Null(await verification.Customers.FindAsync(customer.Id));
    }

    [Fact]
    public async Task InnerScope_CannotCommitOuterTransaction()
    {
        // Arrange
        var products = new Repository<Product, DemoDbContext>(factory);
        var product = new Product { Name = $"nested-{Guid.NewGuid():N}" };

        // Act
        await using (var outer = new UnitOfWorkProvider<DemoDbContext>())
        {
            await using (var inner = new UnitOfWorkProvider<DemoDbContext>())
            {
                await products.AddAsync(product);
                await inner.CommitAsync();
            }

            Assert.NotNull(await products.GetByIdAsync(product.Id));
        }

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Null(await verification.Products.FindAsync(product.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AfterDisposal_RepositoryUsesFreshContext(bool commit)
    {
        // Arrange
        var customers = new Repository<Customer, DemoDbContext>(factory);
        var customer = new Customer { Email = $"cleanup-{Guid.NewGuid():N}@example.test" };

        // Act
        await using (var uow = new UnitOfWorkProvider<DemoDbContext>())
        {
            await customers.AddAsync(customer);
            if (commit)
                await uow.CommitAsync();
        }

        var saved = await customers.GetByIdAsync(customer.Id);

        // Assert
        Assert.Equal(commit, saved is not null);
    }

    [Fact]
    public async Task WithoutOuterScope_RepositoryCommitsItsOwnWrite()
    {
        // Arrange
        var products = new Repository<Product, DemoDbContext>(factory);

        // Act
        var product = await products.AddAsync(new Product { Name = $"auto-{Guid.NewGuid():N}" });

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.NotNull(await verification.Products.FindAsync(product.Id));
    }

    [Fact]
    public async Task ParallelCalls_UseIndependentUnitsOfWork()
    {
        // Arrange
        var products = new Repository<Product, DemoDbContext>(factory);
        var names = Enumerable.Range(0, 3).Select(_ => $"parallel-{Guid.NewGuid():N}").ToArray();

        // Act
        await Task.WhenAll(names.Select(async name =>
        {
            await using var uow = new UnitOfWorkProvider<DemoDbContext>();
            await products.AddAsync(new Product { Name = name });
            await uow.CommitAsync();
        }));

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(3, await verification.Products.CountAsync(product => names.Contains(product.Name)));
    }

    [Fact]
    public async Task CancelledContextCreation_PropagatesCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var uow = new UnitOfWorkProvider<DemoDbContext>();
        var products = new Repository<Product, DemoDbContext>(factory);

        // Act
        var exception = await Record.ExceptionAsync(
            () => products.AddAsync(new Product { Name = "cancelled" }, cancellation.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }
}
