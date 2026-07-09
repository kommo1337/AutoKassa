# Checklist: Add or fix tests in AutoKassa

> См. также: [`TASK_TEMPLATES.md`](../TASK_TEMPLATES.md), skill `.kimi/skills/autokassa-test/SKILL.md`.

---

## Before starting
- [ ] Read [`12-tests.md`](../12-tests.md).
- [ ] Read [`PRINCIPLES.md`](../PRINCIPLES.md).
- [ ] Read [`AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs).

## Test infrastructure
- [ ] Use `TestDatabase.Create()` when the test needs a raw `AppDbContext`.
- [ ] Use `TestDatabase.CreateWithFactory()` when the tested service uses `IDbContextFactory<AppDbContext>`.
- [ ] Store `SqliteConnection` as a test class field and dispose it in `IDisposable.Dispose()`.
- [ ] Use `await using var _ = connection;` pattern where appropriate.

## Seed helpers
- [ ] Use `TestDatabase.SeedExpenseCategory(ctx, name)` / `SeedIncomeCategory(ctx, name)` for categories.
- [ ] Use `TestDatabase.SeedTransaction(...)` for operations.
- [ ] Use `TestDatabase.SeedCounterparty(...)`, `SeedCreditCard(...)`, `SeedCreditCardPurchase(...)` where needed.

## Coverage
- [ ] Cover CRUD operations.
- [ ] Cover filtering, sorting, pagination.
- [ ] Cover edge cases: null, empty strings, negative/zero amounts, boundary values.
- [ ] Cover soft delete and restore behavior.
- [ ] Cover `CancellationToken` propagation.
- [ ] Cover business rules specific to the service (debts, credit cards, reports, etc.).
- [ ] Use `FluentAssertions` for readable assertions.

## Running tests
- [ ] Run `dotnet test --filter "FullyQualifiedName~Services"`.
- [ ] Run full `dotnet test`.
- [ ] Fix any failing tests before finishing.

## Verification
- [ ] All new tests pass.
- [ ] Full `dotnet test` passes.
- [ ] No `SqliteConnection` leaks.
