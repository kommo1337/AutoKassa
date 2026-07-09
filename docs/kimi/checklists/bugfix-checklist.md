# Checklist: Fix a bug in AutoKassa

> См. также: [`TASK_TEMPLATES.md`](../TASK_TEMPLATES.md), skill `.kimi/skills/autokassa-bugfix/SKILL.md`.

---

## Before starting
- [ ] Read [`TRAPS.md`](../TRAPS.md).
- [ ] Read [`06-business-logic.md`](../06-business-logic.md).
- [ ] Read [`json/flows.json`](../json/flows.json) and [`json/services.json`](../json/services.json).

## Reproduction
- [ ] Reproduce the bug via UI, test, or logs.
- [ ] Collect exception message, stack trace, and relevant log entries from `logs/`.
- [ ] Identify affected service / ViewModel / View via `Grep` or `Search`.
- [ ] Check whether the issue relates to a known trap in [`TRAPS.md`](../TRAPS.md).

## Fix
- [ ] Write or update a unit test that reproduces the bug.
- [ ] Apply the minimal fix that resolves the issue.
- [ ] Ensure the fix does not violate [`TRAPS.md`](../TRAPS.md):
  - No business logic in code-behind.
  - No synchronous DB calls.
  - No direct `DbContext` usage from ViewModel.
  - Soft delete for business entities.
  - Correct balance/report calculations excluding `PaymentType.Debt`.
- [ ] Call `_dataChangeService.NotifyDataChanged()` after data mutations if applicable.

## Documentation
- [ ] Update JSON/Markdown if logic or signatures changed.
- [ ] Add a trap to [`TRAPS.md`](../TRAPS.md) and [`json/traps.json`](../json/traps.json) if it is a new common pitfall.

## Verification
- [ ] The repro test fails before the fix and passes after.
- [ ] `dotnet build` passes.
- [ ] `dotnet test` passes.
- [ ] No regression in related tests.
