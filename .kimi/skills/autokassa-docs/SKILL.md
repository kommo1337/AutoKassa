---
name: autokassa-docs
description: Update docs/kimi documentation and JSON references in AutoKassa. Use when the user asks to update documentation, JSON catalogs, docs/kimi, or after code changes require doc sync. Russian triggers: 'документация', 'обнови документацию', 'docs/kimi', 'json справочники', 'синхронизируй доки'.
---

# AutoKassa — Documentation Update

## When this skill triggers
- User asks to update documentation, JSON catalogs, or `docs/kimi/`.
- Code changes require synchronization of docs and JSON references.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/INDEX.md` and `docs/kimi/json/index.json`.

## Steps
1. Identify changed entities, services, ViewModels, flows, and traps.
2. Update JSON catalogs:
   - `models.json`, `services.json`, `viewmodels.json`, `relationships.json`, `flows.json`, `traps.json`, `enums.json`.
3. Update Markdown files:
   - `04-models.md`, `05-services.md`, `07-viewmodels.md`, `08-views.md`, `09-flows.md`, `TRAPS.md`, `INDEX.md`, `kimi.md`.
4. Run `tools/DocValidator`.
5. Run `dotnet build` and `dotnet test`.

## Verification
- [ ] All changed code areas are reflected in JSON.
- [ ] Markdown docs are consistent with code and JSON.
- [ ] Build passes.
- [ ] Tests pass.

## Reference
- [`docs/kimi/checklists/docs-update-checklist.md`](../../docs/kimi/checklists/docs-update-checklist.md)
