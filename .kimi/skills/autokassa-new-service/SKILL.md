---
name: autokassa-new-service
description: Create a new service, interface, or business-logic layer in AutoKassa. Use when the user asks for a new service, business logic, DI registration, or service refactor. Russian triggers: 'сервис', 'новый сервис', 'бизнес-логика', 'добавь сервис', 'создай сервис'.
---

# AutoKassa — New Service

## When this skill triggers
- User asks to create a new service, interface, or business logic.
- User mentions DI registration, service layer, or refactoring services.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/05-services.md`, `PRINCIPLES.md` (DI, async).
3. Read `docs/kimi/json/services.json` and `docs/kimi/json/relationships.json`.

## Steps
1. Create `INewService` and `NewService` in `AutoKassa/Services/`.
2. Register in `App.xaml.cs` with correct lifetime (Singleton/Transient).
3. Inject dependencies via constructor; do not use `App.GetService<T>()`.
4. Use `IDbContextFactory<AppDbContext>` for DB access.
5. Make all public methods `async` with `Async` suffix; use `ConfigureAwait(false)`.
6. Pass `CancellationToken` in public methods where appropriate.
7. Add logging via `Log.ForContext<NewService>()`.
8. Call `_dataChangeService.NotifyDataChanged()` after mutations.
9. Add unit tests in `AutoKassa.Tests/`.
10. Update `json/services.json`, `json/relationships.json`, `05-services.md`.
11. Run `dotnet build` and `dotnet test`.

## Verification
- [ ] Build passes.
- [ ] Tests pass.
- [ ] Service resolves via DI.
- [ ] No synchronous DB calls.
- [ ] JSON/Markdown updated.

## Reference
- [`docs/kimi/checklists/new-service-checklist.md`](../../../docs/kimi/checklists/new-service-checklist.md)
