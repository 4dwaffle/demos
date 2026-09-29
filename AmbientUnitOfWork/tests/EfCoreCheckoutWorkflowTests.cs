using AmbientUnitOfWork.Tests.Infrastructure;
using AmbientUnitOfWork.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AmbientUnitOfWork.Tests;

[Collection("SqlServer")]
public sealed class EfCoreCheckoutWorkflowTests(SqlServerFixture fixture)
{
    private readonly IDbContextFactory<DemoDbContext> factory = fixture.Factory;

    [Fact]
    public async Task WithoutTransaction_FailedReservationLeavesOrderBehind()
    {
        var sku = $"ef-checkout-{Guid.NewGuid():N}";

        await using (var context = await factory.CreateDbContextAsync())
        {
            var checkout = CreateCheckout(context);
            await Assert.ThrowsAsync<OutOfStockException>(() => checkout.PlaceOrderAsync(sku, inStock: false));
        }

        await using var verification = await factory.CreateDbContextAsync();
        Assert.NotNull(await verification.Orders.SingleOrDefaultAsync(order => order.Sku == sku));
        Assert.False(await verification.InventoryReservations.AnyAsync(reservation => reservation.Sku == sku));
    }

    [Fact]
    public async Task ExplicitTransaction_FailedReservationRollsBackOrder()
    {
        var sku = $"ef-checkout-{Guid.NewGuid():N}";

        await using (var context = await factory.CreateDbContextAsync())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var checkout = CreateCheckout(context);
            await Assert.ThrowsAsync<OutOfStockException>(() => checkout.PlaceOrderAsync(sku, inStock: false));
        }

        await using var verification = await factory.CreateDbContextAsync();
        Assert.False(await verification.Orders.AnyAsync(order => order.Sku == sku));
        Assert.False(await verification.InventoryReservations.AnyAsync(reservation => reservation.Sku == sku));
    }

    [Fact]
    public async Task ExplicitTransaction_SuccessCommitsOrderAndReservation()
    {
        var sku = $"ef-checkout-{Guid.NewGuid():N}";

        await using (var context = await factory.CreateDbContextAsync())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var checkout = CreateCheckout(context);
            await checkout.PlaceOrderAsync(sku, inStock: true);
            await transaction.CommitAsync();
        }

        await using var verification = await factory.CreateDbContextAsync();
        var order = await verification.Orders.SingleAsync(order => order.Sku == sku);
        var reservation = await verification.InventoryReservations.SingleAsync(reservation => reservation.Sku == sku);
        Assert.Equal(order.Id, reservation.OrderId);
    }

    private static CheckoutWorkflow CreateCheckout(DemoDbContext context)
    {
        var orders = new OrderService(context);
        var inventory = new InventoryService(context);
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

    private sealed class OrderService(DemoDbContext context)
    {
        public async Task<Order> CreateAsync(string sku)
        {
            var order = new Order { Sku = sku };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return order;
        }
    }

    private sealed class InventoryService(DemoDbContext context)
    {
        public async Task ReserveAsync(int orderId, string sku, bool inStock)
        {
            if (!inStock)
                throw new OutOfStockException();

            context.InventoryReservations.Add(new InventoryReservation { OrderId = orderId, Sku = sku });
            await context.SaveChangesAsync();
        }
    }

    private sealed class OutOfStockException : Exception;
}
