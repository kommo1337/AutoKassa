---
name: autokassa-bugfix
description: Fix a bug, exception, or incorrect behavior in AutoKassa. Use when the user reports an error, crash, broken feature, or asks to fix something. Russian triggers: 'баг', 'исправь', 'ошибка', 'падает', 'не работает', 'исключение', 'исправить баг'.
---

# AutoKassa — Bugfix

## When this skill triggers
- User reports a bug, exception, or incorrect behavior.
- User asks to fix, repair, or investigate why something does not work.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/TRAPS.md`, `06-business-logic.md`.
3. Read `docs/kimi/json/flows.json` and `docs/kimi/json/services.json`.

## Steps
1. Reproduce the issue via test, UI, or logs in `logs/`.
2. Locate affected service/VM/View using `Grep` or `Search`.
3. Check whether the root cause is a known trap from `TRAPS.md`.
4. Write or update a test that reproduces the bug.
5. Apply the minimal fix without violating MVVM, async, DI, or soft-delete rules.
6. Update JSON/Markdown if logic or signatures changed.
7. Add a new trap to `TRAPS.md` and `json/traps.json` if it is a common pitfall.
8. Run `dotnet build` and `dotnet test`.

## Verification
- [ ] Repro test fails before the fix and passes after.
- [ ] Build passes.
- [ ] Tests pass.
- [ ] Fix respects `TRAPS.md` rules.

## Reference
- [`docs/kimi/checklists/bugfix-checklist.md`](../../docs/kimi/checklists/bugfix-checklist.md)
