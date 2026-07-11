---
name: autokassa-review
description: Review AutoKassa code for compliance with project conventions (MVVM, DI, async/await, soft delete, docs sync). Use when the user asks for code review, conventions check, or PR review. Russian triggers: 'ревью', 'проверь код', 'конвенции', 'соответствует ли', 'code review', 'review'.
---

# AutoKassa — Code Review

## When this skill triggers
- User asks to review code, PR, or check compliance with conventions.
- User mentions code review, conventions, or checking quality.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/PRINCIPLES.md`, `TRAPS.md`.
3. Read `docs/kimi/json/relationships.json`.

## Steps
1. Check MVVM: business logic is in services, not code-behind.
2. Check DI: dependencies injected via constructor, correct lifetime, no `App.GetService<T>()`.
3. Check async/await: public methods end with `Async`, `ConfigureAwait(false)` in services, no sync DB calls.
4. Check soft delete: business entities use `IsDeleted = true`, no physical deletes.
5. Check `IDataChangeService.NotifyDataChanged()` after mutations.
6. Check that `docs/kimi/` JSON/Markdown are updated for changed code.
7. Run `dotnet build` and `dotnet test`.

## Verification
- [ ] No business logic in code-behind.
- [ ] DI and lifetimes are correct.
- [ ] Async rules followed.
- [ ] Soft delete respected.
- [ ] Docs are synchronized.
- [ ] Build passes.
- [ ] Tests pass.

## Reference
- [`docs/kimi/checklists/bugfix-checklist.md`](../../docs/kimi/checklists/bugfix-checklist.md) (for trap checks)
