using Microsoft.EntityFrameworkCore;

namespace AmbientUnitOfWork;

public sealed class Repository<TEntity, TDbContext>(IDbContextFactory<TDbContext> contextFactory)
    where TEntity : class
    where TDbContext : DbContext
{
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await using var scope = new UnitOfWorkProvider<TDbContext>();
        var context = await scope.Get().GetOrCreateContextAsync(contextFactory, cancellationToken);
        var entry = await context.Set<TEntity>().AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync();
        return entry.Entity;
    }

    public async Task<TEntity?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default)
    {
        var ambient = UnitOfWorkProvider<TDbContext>.Current;
        if (ambient is not null)
        {
            var context = await ambient.GetOrCreateContextAsync(contextFactory, cancellationToken);
            return await context.Set<TEntity>().FindAsync([id], cancellationToken);
        }

        await using var readContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await readContext.Set<TEntity>().FindAsync([id], cancellationToken);
    }
}
