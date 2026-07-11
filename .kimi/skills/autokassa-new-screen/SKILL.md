---
name: autokassa-new-screen
description: Add a new screen (ViewModel + View + navigation) to the AutoKassa WPF app. Use when the user asks for a new page, screen, view, window, sidebar item, navigation, or menu. Russian triggers: 'новый экран', 'добавить экран', 'новая страница', 'новое окно', 'пункт меню', 'сайдбар', 'навигация'.
---

# AutoKassa — New Screen

## When this skill triggers
- User asks to add a new screen, page, view, window, or sidebar item.
- User mentions navigation, MainWindow, sidebar, or menu.

## Before changing anything
1. Read `docs/kimi/kimi.md`, `PRINCIPLES.md`, `TRAPS.md`, `INDEX.md`.
2. Read `docs/kimi/00-quickstart.md`, `02-architecture.md`, `07-viewmodels.md`, `08-views.md`.
3. Read `docs/kimi/json/relationships.json` and `docs/kimi/json/viewmodels.json`.

## Steps
1. Create `NewScreenViewModel : ViewModelBase` in `AutoKassa/ViewModels/`.
2. Create `NewScreenView.xaml` (UserControl) in `AutoKassa/Views/`.
3. Register VM in DI in `App.xaml.cs`: `services.AddTransient<NewScreenViewModel>()`.
4. Add `DataTemplate` in `MainWindow.xaml`.
5. Add navigation command in `MainWindowViewModel` and button in sidebar if required.
6. Implement async loading via `IAsyncInitializable` or `RunAsync`.
7. Subscribe to `_dataChangeService.DataChanged`; unsubscribe in `OnDispose()`.
8. Update `json/viewmodels.json` and `json/relationships.json`.
9. Describe the screen in `07-viewmodels.md` and `08-views.md`.
10. Run `dotnet build` and `dotnet test`.

## Verification
- [ ] Build passes.
- [ ] Tests pass.
- [ ] New screen is reachable from sidebar/navigation.
- [ ] JSON/Markdown updated.

## Reference
- [`docs/kimi/checklists/new-screen-checklist.md`](../../docs/kimi/checklists/new-screen-checklist.md)
