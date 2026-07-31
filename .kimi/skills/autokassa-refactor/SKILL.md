---
name: autokassa-refactor
description: Refactor, rename, clean up, or simplify code in AutoKassa without changing behavior. Use when the user asks for refactoring, renaming, cleanup, or simplification. Russian triggers: 'рефакторинг', 'переименуй', 'упрости', 'почисти', 'refactor'.
---

# AutoKassa — Refactor

## When this skill triggers
- User asks to refactor, rename, clean up, or simplify code.
- The goal is to improve structure without changing behavior.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/PRINCIPLES.md`, `TRAPS.md`.
3. Read `docs/kimi/json/relationships.json`.

## Steps
1. Confirm that tests exist for the code being refactored.
2. Make minimal, behavior-preserving changes.
3. For renaming, update all references (code, tests, XAML, JSON docs).
4. Preserve async/await, `ConfigureAwait(false)`, DI lifetime, and MVVM boundaries.
5. Run `dotnet build` and `dotnet test`.
6. Update `json/relationships.json`, `services.json`, `viewmodels.json`, or `models.json` if names or dependencies changed.

## Verification
- [ ] Build passes.
- [ ] Full test suite passes.
- [ ] No behavioral changes introduced.
- [ ] JSON/Markdown updated if names or dependencies changed.

## Reference
- [`docs/kimi/checklists/refactor-checklist.md`](../../../docs/kimi/checklists/refactor-checklist.md)
