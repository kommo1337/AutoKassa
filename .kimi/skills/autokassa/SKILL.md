---
name: autokassa
description: Load AutoKassa project context for every task in the c:\Users\Лиза Блинова\source\repos\kommo1337\AutoKassa repository. Enforces WPF/SQLite/.NET 10 conventions — MVVM, DI, async-only DB access, soft delete, Russian UI/comments. Use automatically for any C#, XAML, EF Core, migration, test, or documentation task in this repo. Triggers — English: 'autokassa', 'wpf finance app', 'sqlite', '.net 10'. Russian: 'автокасса', 'wpf финансы', 'sqlite', '.net 10'. For typical tasks prefer narrower skills — new screen, entity, service, bugfix, test, refactor, docs, review — from the same skills folder.
---

# AutoKassa — Meta Skill

## When this skill triggers
- Any code, test, migration, or documentation task in the AutoKassa repository.
- User mentions AutoKassa, WPF finance app, SQLite, .NET 10, автокасса.
- No narrower project skill (`autokassa-new-screen`, `autokassa-bugfix`, etc.) is a better fit.

## Before changing anything
1. Read `docs/kimi/kimi.md` for entry rules and the «Я хочу…» map.
2. Read `docs/kimi/PRINCIPLES.md` for coding principles.
3. Read `docs/kimi/TRAPS.md` for common pitfalls and prohibitions.
4. Read `docs/kimi/INDEX.md` for full documentation map.

## Steps
1. Identify the task type and activate the most specific project skill if available:
   - New screen / page / window / sidebar item → `autokassa-new-screen`.
   - New entity / migration / model change → `autokassa-new-entity`.
   - New service / business logic → `autokassa-new-service`.
   - Bug / exception / incorrect behavior → `autokassa-bugfix`.
   - Tests / coverage / test infrastructure → `autokassa-test`.
   - Refactor / rename / cleanup → `autokassa-refactor`.
   - Documentation / docs sync → `autokassa-docs`.
   - Code review / conventions check → `autokassa-review`.
2. Follow the activated skill's checklist from `docs/kimi/checklists/`.
3. If no specific skill fits, still apply the core rules from this skill:
   - Keep business logic in services, not in View code-behind.
   - Use constructor injection; do not use `App.GetService<T>()` from ViewModel or service.
   - All DB access is async via `IDbContextFactory<AppDbContext>`.
   - Use soft delete (`IsDeleted = true`) for business entities.
   - Call `_dataChangeService.NotifyDataChanged()` after data mutations.
   - Update the relevant JSON and Markdown files in `docs/kimi/`.
   - Run `dotnet build` and `dotnet test` after changes.

## Verification
- [ ] `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md` were read.
- [ ] Correct narrower skill was activated when applicable.
- [ ] Core project rules (MVVM, DI, async, soft delete, docs sync) are followed.
- [ ] `dotnet build` passes.
- [ ] `dotnet test` passes.

## References
- [`references/principles-summary.md`](references/principles-summary.md) — condensed principles.
- [`references/traps-summary.md`](references/traps-summary.md) — condensed traps.
