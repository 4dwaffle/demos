using AmbientUnitOfWork.Tests.Models;
using Microsoft.EntityFrameworkCore;

namespace AmbientUnitOfWork.Tests.Infrastructure;

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