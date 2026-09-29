# Ambient unit of work

This runnable EF Core example uses a real SQL Server transaction and two
repositories. The integration tests demonstrate commit, rollback, nested scopes,
ambient cleanup, parallel calls, and cancellation. `EfCoreTransactionTests` show
the same commit and rollback behavior using EF Core transactions directly.

## Run

Install the .NET 10 SDK and start Docker, then from this directory run:

```sh
dotnet test AmbientUnitOfWork.slnx
```

Testcontainers starts SQL Server 2022. The first run downloads its image.

## Walkthrough

```csharp
var products = new Repository<Product, DemoDbContext>(factory);
var customers = new Repository<Customer, DemoDbContext>(factory);

await using var uow = new UnitOfWorkProvider<DemoDbContext>();
await products.AddAsync(new Product { Name = "Keyboard" });
await customers.AddAsync(new Customer { Email = "student@example.test" });
await uow.CommitAsync();
```

1. The outer provider puts a `UnitOfWork<DemoDbContext>` in `AsyncLocal`.
2. Each repository opens an inner provider. It finds the same ambient unit of
   work and shares one `DbContext` and SQL transaction.
3. Each `AddAsync` calls `SaveChangesAsync` to send SQL to the database. The
   changes still are **not committed**. Inner providers cannot commit the
   transaction owned by the outer provider.
4. The outer `CommitAsync` commits both writes. Disposing without committing
   rolls them back. Disposing also clears the ambient value, so the next call
   starts with a fresh context.

Open the tests in this order: `Commit_PersistsWritesFromTwoRepositories`,
`DisposingWithoutCommit_RollsBackSavedChanges`,
`ExceptionBeforeCommit_RollsBackBothRepositories`,
`InnerScope_CannotCommitOuterTransaction`, then
`AfterDisposal_RepositoryUsesFreshContext`.

For a comparison without the ambient provider, open `EfCoreTransactionTests`.
Those tests create a `DbContext`, call `BeginTransactionAsync`, save both entities,
and either commit or dispose the transaction.

## Why use the ambient scope?

Open `CheckoutWorkflowTests`. The checkout workflow calls an order service and
an inventory service. Each service owns a repository; neither receives a
`DbContext` or transaction from the workflow.

- Without an outer scope, the order repository commits its write. If stock is
  unavailable, the inventory service throws and the order remains in the database.
- With an outer scope, the order write and the reservation share one transaction.
  The same failure rolls back the order; a successful checkout commits both.

The stock failure is simulated so the difference is deterministic. Both services
use the **same database**. This example does not provide an atomic transaction
across separate databases.

For the EF-only version of the same checkout, open `EfCoreCheckoutWorkflowTests`.
The caller creates one `DbContext`, passes it to both services, and uses
`BeginTransactionAsync` and `CommitAsync` for the atomic case. Its three tests
show the same partial write, rollback, and successful commit. This is a simpler
choice when sharing one `DbContext` explicitly fits the application design.

**Scope:** A unit of work is keyed by one `DbContext` type and represents one
database transaction. `AsyncLocal` flows across `await`; it does not make
separate databases or independent `DbContext` types atomic. Do not run
concurrent repository calls inside the *same* ambient scope because they would
share a `DbContext`, which is not thread safe. `ParallelCalls` starts a new
outer scope in each independent task.
