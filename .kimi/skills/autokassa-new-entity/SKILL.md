---
name: autokassa-new-entity
description: Add or change an EF Core entity, table, field, relationship, or migration in AutoKassa. Use for model changes and schema updates. Russian triggers: 'сущность', 'миграция', 'добавь поле', 'новая таблица', 'измени модель', 'новая сущность'.
---

# AutoKassa — New Entity + Migration

## When this skill triggers
- User asks to add/change an entity, field, table, relationship, or migration.
- User mentions EF Core, database schema, or model change.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/04-models.md`, `03-database.md`, `06-business-logic.md`.
3. Read `docs/kimi/json/models.json` and `docs/kimi/json/relationships.json`.

## Steps
1. Check dependencies through `json/relationships.json`.
2. Create or modify the entity class in `AutoKassa/Models/`.
3. Add/update `DbSet<T>` and `OnModelCreating` in `AutoKassa/Services/AppDbContext.cs`.
4. Create migration: `cd AutoKassa && dotnet ef migrations add <Name>`.
5. Verify `Up()` / `Down()`; test on a fresh database.
6. Update DTO/VM/View if the entity is exposed in UI.
7. Update `json/models.json`, `json/relationships.json`, `04-models.md`, `03-database.md`.
8. Run `dotnet build` and `dotnet test`.

## Verification
- [ ] Migration applies cleanly.
- [ ] Build passes.
- [ ] Tests pass.
- [ ] App starts on a fresh DB without schema errors.
- [ ] JSON/Markdown updated.

## Reference
- [`docs/kimi/checklists/new-entity-checklist.md`](../../../docs/kimi/checklists/new-entity-checklist.md)
