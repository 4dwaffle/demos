using AmbientUnitOfWork.Tests.Infrastructure;
using AmbientUnitOfWork.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AmbientUnitOfWork.Tests;

[Collection("SqlServer")]
public sealed class CheckoutWorkflowTests(SqlServerFixture fixture)
{
    private readonly IDbContextFactory<DemoDbContext> factory = fixture.Factory;

    [Fact]
    public async Task WithoutAmbientScope_FailedReservationLeavesOrderBehind()
    {
        // Arrange
        var sku = $"checkout-{Guid.NewGuid():N}";
        var checkout = CreateCheckout();

        // Act
        await Assert.ThrowsAsync<OutOfStockException>(() => checkout.PlaceOrderAsync(sku, inStock: false));

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.NotNull(await verification.Orders.SingleOrDefaultAsync(order => order.Sku == sku));
        Assert.Empty(await verification.InventoryReservations.Where(reservation => reservation.Sku == sku).ToListAsync());
    }

    [Fact]
    public async Task AmbientScope_FailedReservationRollsBackOrder()
    {
        // Arrange
        var sku = $"checkout-{Guid.NewGuid():N}";
        var checkout = CreateCheckout();

        // Act
        await Assert.ThrowsAsync<OutOfStockException>(async () =>
        {
            await using var uow = new UnitOfWorkProvider<DemoDbContext>();
            await checkout.PlaceOrderAsync(sku, inStock: false);
            await uow.CommitAsync();
        });

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Empty(await verification.Orders.Where(order => order.Sku == sku).ToListAsync());
        Assert.Empty(await verification.InventoryReservations.Where(reservation => reservation.Sku == sku).ToListAsync());
    }

    [Fact]
    public async Task AmbientScope_SuccessCommitsOrderAndReservation()
    {
        // Arrange
        var sku = $"checkout-{Guid.NewGuid():N}";
        var checkout = CreateCheckout();

        // Act
        await using (var uow = new UnitOfWorkProvider<DemoDbContext>())
        {
            await checkout.PlaceOrderAsync(sku, inStock: true);
            await uow.CommitAsync();
        }

        // Assert
        await using var verification = await factory.CreateDbContextAsync();
        var order = await verification.Orders.SingleAsync(order => order.Sku == sku);
        var reservation = await verification.InventoryReservations.SingleAsync(reservation => reservation.Sku == sku);
        Assert.Equal(order.Id, reservation.OrderId);
    }

    private CheckoutWorkflow CreateCheckout()
    {
        var orders = new OrderService(new Repository<Order, DemoDbContext>(factory));
        var inventory = new InventoryService(new Repository<InventoryReservation, DemoDbContext>(factory));
        return new CheckoutWorkflow(orders, inventory);
    }

    private sealed class CheckoutWorkflow(OrderService orders, InventoryService inventory)
    {
        public async Task PlaceOrderAsync(string sku, bool inStock)
        {
            var order = await orders.CreateAsync(sku);
            await inventory.ReserveAsync(order.Id, sku, inStock);
        }
    }

    private sealed class OrderService(Repository<Order, DemoDbContext> orders)
    {
        public Task<Order> CreateAsync(string sku) => orders.AddAsync(new Order { Sku = sku });
    }

    private sealed class InventoryService(Repository<InventoryReservation, DemoDbContext> reservations)
    {
        public async Task ReserveAsync(int orderId, string sku, bool inStock)
        {
            if (!inStock)
                throw new OutOfStockException();

            await reservations.AddAsync(new InventoryReservation { OrderId = orderId, Sku = sku });
        }
    }

    private sealed class OutOfStockException : Exception;
}
