# Checklist: Add a new entity + migration to AutoKassa

> См. также: [`TASK_TEMPLATES.md`](../TASK_TEMPLATES.md), skill `.kimi/skills/autokassa-new-entity/SKILL.md`.

---

## Before starting
- [ ] Read [`PRINCIPLES.md`](../PRINCIPLES.md).
- [ ] Read [`TRAPS.md`](../TRAPS.md).
- [ ] Read [`04-models.md`](../04-models.md) and [`03-database.md`](../03-database.md).
- [ ] Read [`json/models.json`](../json/models.json) and [`json/relationships.json`](../json/relationships.json).

## Implementation
- [ ] Check dependencies through [`json/relationships.json`](../json/relationships.json).
- [ ] Create/change entity class in `AutoKassa/Models/`.
- [ ] Add/update `DbSet<T>` in `AutoKassa/Services/AppDbContext.cs`.
- [ ] Configure relationships, indexes, constraints in `OnModelCreating`.
- [ ] Add migration: `cd AutoKassa && dotnet ef migrations add <Name>`.
- [ ] Verify migration `Up()` / `Down()` looks correct.
- [ ] Update DTO/VM/View if the entity is exposed in UI.
- [ ] Include `IsDeleted` flag for business entities that require soft delete.
- [ ] Avoid cascade delete unless explicitly approved.

## Documentation
- [ ] Update [`json/models.json`](../json/models.json).
- [ ] Update [`json/relationships.json`](../json/relationships.json) if new relationships appear.
- [ ] Update [`04-models.md`](../04-models.md).
- [ ] Update [`03-database.md`](../03-database.md).

## Verification
- [ ] Migration applies cleanly on a fresh database.
- [ ] `dotnet build` passes.
- [ ] `dotnet test` passes.
- [ ] App starts without schema mismatch errors.
- [ ] Soft-delete rules are respected (do not use physical delete for business entities).
