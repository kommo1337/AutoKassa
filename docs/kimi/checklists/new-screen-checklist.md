# Checklist: Add a new screen to AutoKassa

> См. также: [`TASK_TEMPLATES.md`](../TASK_TEMPLATES.md), skill `.kimi/skills/autokassa-new-screen/SKILL.md`.

---

## Before starting
- [ ] Read [`PRINCIPLES.md`](../PRINCIPLES.md).
- [ ] Read [`TRAPS.md`](../TRAPS.md).
- [ ] Read [`00-quickstart.md`](../00-quickstart.md) → «Чек-лист: добавить новый экран».
- [ ] Read [`02-architecture.md`](../02-architecture.md), [`07-viewmodels.md`](../07-viewmodels.md), [`08-views.md`](../08-views.md).
- [ ] Read [`json/relationships.json`](../json/relationships.json) and [`json/viewmodels.json`](../json/viewmodels.json).

## Implementation
- [ ] Create `NewScreenViewModel : ViewModelBase` in `AutoKassa/ViewModels/`.
- [ ] Create `NewScreenView.xaml` (UserControl) in `AutoKassa/Views/`.
- [ ] Register VM in DI in `AutoKassa/App.xaml.cs` (`services.AddTransient<NewScreenViewModel>()`).
- [ ] Add `DataTemplate` in `AutoKassa/MainWindow.xaml`.
- [ ] Add navigation command in `MainWindowViewModel`.
- [ ] Add sidebar button in `MainWindow.xaml` if required.
- [ ] Implement async data loading via `IAsyncInitializable` or `RunAsync`.
- [ ] Use commands (`RelayCommand` / `AsyncRelayCommand`) for user actions.
- [ ] Use `INotifyDataErrorInfo` for validation if needed.
- [ ] Subscribe to `_dataChangeService.DataChanged` and unsubscribe in `OnDispose()`.
- [ ] No business logic in code-behind of the View.

## Documentation
- [ ] Update [`json/viewmodels.json`](../json/viewmodels.json).
- [ ] Update [`json/relationships.json`](../json/relationships.json).
- [ ] Update [`07-viewmodels.md`](../07-viewmodels.md).
- [ ] Update [`08-views.md`](../08-views.md).

## Verification
- [ ] `dotnet build` passes.
- [ ] `dotnet test` passes.
- [ ] New screen is reachable from sidebar/navigation.
- [ ] Data updates when `IDataChangeService.NotifyDataChanged()` is fired from another screen.
- [ ] ViewModel is disposed and unsubscribed on navigation away.
