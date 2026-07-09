# Checklist: Update documentation in AutoKassa

> См. также: [`TASK_TEMPLATES.md`](../TASK_TEMPLATES.md), skill `.kimi/skills/autokassa-docs/SKILL.md`.

---

## Before starting
- [ ] Read [`INDEX.md`](../INDEX.md).
- [ ] Read [`json/index.json`](../json/index.json).
- [ ] Identify which code changes triggered the docs update.

## JSON updates
Update the corresponding JSON references:
- [ ] [`json/models.json`](../json/models.json) — if entities changed.
- [ ] [`json/services.json`](../json/services.json) — if services, lifetimes, or dependencies changed.
- [ ] [`json/viewmodels.json`](../json/viewmodels.json) — if ViewModels changed.
- [ ] [`json/relationships.json`](../json/relationships.json) — if graph of dependencies changed.
- [ ] [`json/flows.json`](../json/flows.json) — if user flows changed.
- [ ] [`json/traps.json`](../json/traps.json) — if new traps or restrictions were introduced.
- [ ] [`json/enums.json`](../json/enums.json) — if enums changed.

## Markdown updates
- [ ] [`04-models.md`](../04-models.md) — entity descriptions.
- [ ] [`05-services.md`](../05-services.md) — service catalog.
- [ ] [`07-viewmodels.md`](../07-viewmodels.md) — ViewModel descriptions.
- [ ] [`08-views.md`](../08-views.md) — View/XAML descriptions.
- [ ] [`09-flows.md`](../09-flows.md) — user scenarios.
- [ ] [`TRAPS.md`](../TRAPS.md) — new traps and restrictions.
- [ ] [`INDEX.md`](../INDEX.md) — table of contents if new sections added.
- [ ] [`kimi.md`](../kimi.md) — «Я хочу…» map if new scenarios added.

## Validation
- [ ] Cross-check docs against actual code.
- [ ] Run `tools/DocValidator` if available (Stage 5).
- [ ] Run `dotnet build`.
- [ ] Run `dotnet test`.

## Verification
- [ ] All changed code areas are reflected in JSON docs.
- [ ] Markdown docs do not contradict code or other docs.
- [ ] Build and tests pass.
