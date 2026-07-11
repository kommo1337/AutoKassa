---
name: autokassa-test
description: Add or fix unit tests in AutoKassa using xUnit and FluentAssertions. Use when the user asks for tests, coverage, test infrastructure, or fixing failing tests. Russian triggers: 'тест', 'тесты', 'покрой тестами', 'покрытие', 'xunit', 'fluentassertions'.
---

# AutoKassa — Tests

## When this skill triggers
- User asks to add tests, increase coverage, or fix test infrastructure.
- User mentions xUnit, FluentAssertions, test database, or coverage.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/12-tests.md`.
3. Read `AutoKassa.Tests/Infrastructure/TestDatabase.cs`.

## Steps
1. Use `TestDatabase.Create()` or `TestDatabase.CreateWithFactory()`.
2. Keep `SqliteConnection` open during the test; dispose in `IDisposable.Dispose()`.
3. Use seed helpers: `SeedExpenseCategory`, `SeedIncomeCategory`, `SeedTransaction`, etc.
4. Cover CRUD, filtering, edge cases, `CancellationToken`, soft delete, and business rules.
5. Use `FluentAssertions` for readable assertions.
6. Run `dotnet test --filter "FullyQualifiedName~Services"`.
7. Run full `dotnet test`.

## Verification
- [ ] Service tests pass.
- [ ] Full test suite passes.
- [ ] No `SqliteConnection` leaks.
- [ ] New tests cover the requested scenarios.

## Reference
- [`docs/kimi/checklists/test-checklist.md`](../../docs/kimi/checklists/test-checklist.md)
