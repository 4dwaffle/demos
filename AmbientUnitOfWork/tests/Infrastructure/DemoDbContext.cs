using AmbientUnitOfWork.Tests.Models;
using Microsoft.EntityFrameworkCore;

namespace AmbientUnitOfWork.Tests.Infrastructure;

public sealed class DemoDbContext(DbContextOptions<DemoDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().Property(product => product.Name).HasMaxLength(200);
        modelBuilder.Entity<Customer>().Property(customer => customer.Email).HasMaxLength(256);
        modelBuilder.Entity<Order>().Property(order => order.Sku).HasMaxLength(100);
        modelBuilder.Entity<InventoryReservation>().Property(reservation => reservation.Sku).HasMaxLength(100);
        modelBuilder.Entity<InventoryReservation>()
            .HasOne<Order>()
            .WithMany()
            .HasForeignKey(reservation => reservation.OrderId);
    }
}
