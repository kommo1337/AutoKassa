# Checklist: Refactor in AutoKassa

> См. также: [`TASK_TEMPLATES.md`](../TASK_TEMPLATES.md), skill `.kimi/skills/autokassa-refactor/SKILL.md`.

---

## Before starting
- [ ] Read [`PRINCIPLES.md`](../PRINCIPLES.md).
- [ ] Read [`TRAPS.md`](../TRAPS.md).
- [ ] Read [`json/relationships.json`](../json/relationships.json).

## Preparation
- [ ] Confirm that tests exist for the code being refactored.
- [ ] Identify the reason for refactoring (duplication, readability, complexity, naming).
- [ ] Avoid refactoring unrelated code in the same commit/change set.

## Refactoring
- [ ] Keep behavior unchanged; if behavior changes, treat it as a bugfix or feature.
- [ ] For renaming, update all references (types, variables, tests, XAML bindings, JSON docs).
- [ ] Preserve async/await patterns and `ConfigureAwait(false)`.
- [ ] Preserve DI lifetime and dependency graph.
- [ ] Keep business logic in services, not in ViewModel or code-behind.

## Documentation
- [ ] Update [`json/relationships.json`](../json/relationships.json) if dependencies changed.
- [ ] Update [`json/services.json`](../json/services.json), [`json/viewmodels.json`](../json/viewmodels.json), or [`json/models.json`](../json/models.json) if names changed.
- [ ] Update relevant Markdown files if structural changes occurred.

## Verification
- [ ] `dotnet build` passes.
- [ ] Full `dotnet test` passes.
- [ ] Code coverage for refactored area is not reduced.
- [ ] No behavioral changes detected by tests.
