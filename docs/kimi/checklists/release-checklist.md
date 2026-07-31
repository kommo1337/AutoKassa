# Checklist: Release / publish AutoKassa

> См. также: skill `.kimi/skills/autokassa/SKILL.md`, [`CHANGELOG.md`](../../../CHANGELOG.md).

---

## Pre-release
- [ ] All features merged and tested.
- [ ] `dotnet build` passes in `Release` configuration.
- [ ] `dotnet test` passes fully.
- [ ] `docs/kimi/` JSON and Markdown are synchronized with code.
- [ ] Version bumped in project files / `metadata.json` / `kimi.md` if required.

## Packaging
- [ ] Publish portable build:
  ```bash
  dotnet publish AutoKassa -c Release -r win-x64 --self-contained true -o ./publish_portable
  ```
- [ ] Verify `publish_portable/AutoKassa.exe` starts.
- [ ] Verify `publish_portable/AutoKassa.db` is created / migrations apply on first run.
- [ ] Create `AutoKassa_Portable.zip` if required.

## Documentation and changelog
- [ ] Update [`CHANGELOG.md`](../../../CHANGELOG.md) with release notes.
- [ ] Update [`README.md`](../../../README.md) if user-facing behavior changed.
- [ ] Update screenshots in `screenshots/` if UI changed significantly.

## Final checks
- [ ] Clean / rebuild solution to remove stale artifacts.
- [ ] Run smoke tests on published executable (add operation, generate report, export PDF/Excel).
- [ ] Confirm no sensitive data (passwords, connection strings) is embedded in published output.

## Verification
- [ ] Published app launches on a clean Windows machine / VM.
- [ ] Core user flows work: login, add transaction, dashboard, reports, settings.
- [ ] Backup and restore functions work.
