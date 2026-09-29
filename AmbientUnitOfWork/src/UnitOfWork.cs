using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AmbientUnitOfWork;

public sealed class UnitOfWork<TDbContext>(Guid ownerId) : IAsyncDisposable where TDbContext : DbContext
{
    public Guid OwnerId => ownerId;
    private TDbContext? DbContext { get; set; }
    private IDbContextTransaction? Transaction { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
            await Transaction.DisposeAsync();

        if (DbContext is not null)
            await DbContext.DisposeAsync();
    }

    internal async Task CommitAsync()
    {
        if (DbContext is not null)
            await DbContext.Database.CommitTransactionAsync();
    }

    internal async Task<TDbContext> GetOrCreateContextAsync(
        IDbContextFactory<TDbContext> factory, CancellationToken cancellationToken)
    {
        if (DbContext is null)
        {
            var context = await factory.CreateDbContextAsync(cancellationToken);
            try
            {
                Transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                DbContext = context;
            }
            catch
            {
                await context.DisposeAsync();
                throw;
            }
        }

        return DbContext;
    }
}
