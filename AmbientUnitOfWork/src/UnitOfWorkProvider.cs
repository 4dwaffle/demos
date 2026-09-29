using Microsoft.EntityFrameworkCore;

namespace AmbientUnitOfWork;

public class UnitOfWorkProvider<TDbContext> : IAsyncDisposable where TDbContext : DbContext
{
    private static readonly AsyncLocal<UnitOfWork<TDbContext>?> Data = new();
    private readonly Guid id = Guid.NewGuid();

    private bool OwnsUnitOfWork => Data.Value?.OwnerId == id;
    internal static UnitOfWork<TDbContext>? Current => Data.Value;

    public UnitOfWorkProvider()
    {
        if (Data.Value is null)
            Data.Value = new UnitOfWork<TDbContext>(id);
    }

    public ValueTask DisposeAsync()
    {
        if (!OwnsUnitOfWork)
            return ValueTask.CompletedTask;

        var unitOfWork = Data.Value!;
        Data.Value = null;
        return unitOfWork.DisposeAsync();
    }

    internal UnitOfWork<TDbContext> Get() => Data.Value
        ?? throw new InvalidOperationException("The ambient unit of work has ended.");

    public Task CommitAsync() => OwnsUnitOfWork
        ? Data.Value!.CommitAsync()
        : Task.CompletedTask;
}
