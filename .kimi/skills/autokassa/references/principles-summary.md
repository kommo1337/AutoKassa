# AutoKassa — Principles Summary

> Короткая выжимка [`docs/kimi/PRINCIPLES.md`](../../../../docs/kimi/PRINCIPLES.md). Используй как шпаргалку внутри skill `autokassa`.

---

## 1. MVVM
- Бизнес-логика только в сервисах.
- ViewModel не знает о View.
- Code-behind View — только минимальная UI-логика.
- Команды: `RelayCommand` / `AsyncRelayCommand`.
- Свойства: `SetProperty<T>` из `ViewModelBase`.

## 2. Dependency Injection
- Все зависимости через конструктор.
- Регистрация в `App.ConfigureServices`.
- Уважай lifetime: `Singleton`, `Scoped`, `Transient`.
- Singleton-сервисы используют `IDbContextFactory<AppDbContext>`.

## 3. База данных
- Только асинхронные операции.
- Создавай `DbContext` через фабрику и не храни долго.
- Для чтения используй `AsNoTracking()`, если не изменяешь.
- Атомарные операции — `BeginTransactionAsync`.

## 4. Async/Await
- Публичные асинхронные методы — суффикс `Async`.
- В сервисах используй `ConfigureAwait(false)`.
- Передавай `CancellationToken` в публичных методах, где уместно.

## 5. Валидация
- ViewModel: `INotifyDataErrorInfo` через `SetErrors` / `ClearErrors`.
- Дополнительные проверки — в сервисах перед сохранением.

## 6. Навигация и жизненный цикл
- `INavigationAware` для реакции на смену вкладки.
- `IAsyncInitializable` / `RunAsync` для асинхронной загрузки.
- Отписывайся от событий и освобождай ресурсы в `Dispose()` / `OnDispose()`.
- После изменений данных — `_dataChangeService.NotifyDataChanged()`.

## 7. Логирование
- `Log.ForContext<T>()`.
- Логируй создание, обновление, удаление операций/категорий.
- Ошибки БД и ФС — с полным исключением.

## 8. Тестирование
- `TestDatabase.Create()` / `CreateWithFactory()`.
- `SqliteConnection` открыт на время теста, `Dispose()` в конце.
- Покрывай новую бизнес-логику юнит-тестами.
