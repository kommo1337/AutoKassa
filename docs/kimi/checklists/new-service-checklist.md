# Checklist: Add a new service to AutoKassa

> См. также: [`TASK_TEMPLATES.md`](../TASK_TEMPLATES.md), skill `.kimi/skills/autokassa-new-service/SKILL.md`.

---

## Before starting
- [ ] Read [`PRINCIPLES.md`](../PRINCIPLES.md).
- [ ] Read [`TRAPS.md`](../TRAPS.md).
- [ ] Read [`05-services.md`](../05-services.md).
- [ ] Read [`json/services.json`](../json/services.json) and [`json/relationships.json`](../json/relationships.json).

## Implementation
- [ ] Create `INewService` interface in `AutoKassa/Services/`.
- [ ] Create `NewService : INewService` implementation.
- [ ] Register in DI in `AutoKassa/App.xaml.cs` with correct lifetime:
  - Singleton: `NavigationService`, `PasswordService`, `SettingsService`, `DialogService`, `ToastNotificationService`, `LockService`, `ExportService`, `DataChangeService`, factories, etc.
  - Transient: most business services.
- [ ] Inject dependencies via constructor; do not use `App.GetService<T>()`.
- [ ] Use `IDbContextFactory<AppDbContext>` instead of holding a `DbContext`.
- [ ] All public methods are `async` with `Async` suffix.
- [ ] Use `ConfigureAwait(false)` in service code.
- [ ] Pass `CancellationToken` in public methods where appropriate.
- [ ] Add logging via `private static readonly ILogger _log = Log.ForContext<NewService>();`.
- [ ] Call `_dataChangeService.NotifyDataChanged()` after data mutations.

## Tests
- [ ] Add unit tests in `AutoKassa.Tests/`.
- [ ] Use `TestDatabase.CreateWithFactory()` for services using `IDbContextFactory`.
- [ ] Keep `SqliteConnection` open during test; dispose in `IDisposable.Dispose()`.
- [ ] Cover happy path, edge cases, and `CancellationToken` propagation.

## Documentation
- [ ] Update [`json/services.json`](../json/services.json).
- [ ] Update [`json/relationships.json`](../json/relationships.json).
- [ ] Update [`05-services.md`](../05-services.md).

## Verification
- [ ] `dotnet build` passes.
- [ ] `dotnet test` passes.
- [ ] Service resolves correctly through DI.
- [ ] No synchronous DB calls or `.Result`/`.Wait()` usages.
