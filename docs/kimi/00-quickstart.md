# 00-quickstart — 5-минутный обзор AutoKassa

> Этот файл поможет быстро понять проект и начать работу. Для глубокого погружения читай [`01-overview.md`](01-overview.md), [`02-architecture.md`](02-architecture.md) и JSON-справочники.

---

## Что такое AutoKassa

AutoKassa — десктопное WPF-приложение для учёта финансовых операций автосервиса. Работает локально на Windows, хранит данные в SQLite, не требует облака.

**Ключевые возможности:**
- Учёт доходов и расходов по категориям.
- Разделение по типу оплаты: наличные, безналичные, кредитная карта, долг.
- Дашборд со сводкой, графиком и последними операциями.
- Справочники категорий и контрагентов.
- Кредитные карты с FIFO-погашением покупок.
- Долги (дебиторка/кредиторка) с частичными погашениями.
- Отчёты (баланс, категории, детализация, долги) с экспортом в PDF и Excel.
- Настройки, резервное копирование БД, защита паролем и автоблокировка.

---

## Технологический стек

| Компонент | Технология | Версия |
|-----------|-----------|--------|
| Платформа | .NET | 10.0-windows |
| UI | WPF | — |
| ORM | Entity Framework Core | 10.0.8 |
| База данных | SQLite | — |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.8 |
| Графики | OxyPlot.Wpf | 2.2.0 |
| PDF | QuestPDF | 2026.5.0 |
| Excel | ClosedXML | 0.105.0 |
| Хеширование | BCrypt.Net-Next | 4.2.0 |
| Логирование | Serilog + File sink | 4.3.1 / 7.0.0 |
| Тесты | xUnit + FluentAssertions | 2.9.3 / 8.10.0 |

---

## Структура решения

```
AutoKassa.sln
├── AutoKassa/                 # WPF-приложение
│   ├── App.xaml.cs            # Точка входа, DI-контейнер
│   ├── MainWindow.xaml        # Главное окно (сайдбар + контент)
│   ├── Models/                # Сущности и enum
│   ├── Services/              # Бизнес-логика и доступ к данным
│   ├── ViewModels/            # MVVM ViewModels
│   ├── Views/                 # XAML-представления
│   ├── Helpers/               # Базовые классы, конвертеры
│   └── Migrations/            # EF Core миграции
│
├── AutoKassa.Tests/           # Юнит-тесты
│   ├── Infrastructure/TestDatabase.cs
│   └── Services/*Tests.cs
│
└── docs/kimi/                 # Эта документация
```

---

## Как читать эту документацию

1. **Начни с [`kimi.md`](kimi.md)** — базовые правила и карта сценариев.
2. **Избегай ошибок** — прочитай [`TRAPS.md`](TRAPS.md).
3. **Найди нужный сервис/сущность/VM** — используй JSON-справочники:
   - [`json/models.json`](json/models.json) — сущности.
   - [`json/services.json`](json/services.json) — сервисы.
   - [`json/viewmodels.json`](json/viewmodels.json) — ViewModels.
   - [`json/relationships.json`](json/relationships.json) — кто от кого зависит.
4. **Разбери поток** — [`json/flows.json`](json/flows.json) или [`09-flows.md`](09-flows.md).

---

## Чек-лист: добавить новый экран

1. Создать ViewModel, наследующий [`ViewModelBase`](../../AutoKassa/Helpers/ViewModelBase.cs).
2. Создать View (UserControl/XAML).
3. Зарегистрировать VM в DI в [`App.xaml.cs`](../../AutoKassa/App.xaml.cs): page ViewModel вкладки — `services.AddSingleton<NewViewModel>()` + маркер `IPageViewModel`; модальную/диалоговую — `services.AddTransient<NewViewModel>()`.
4. Добавить `DataTemplate` в [`MainWindow.xaml`](../../AutoKassa/MainWindow.xaml).
5. Добавить команду навигации в [`MainWindowViewModel`](../../AutoKassa/ViewModels/MainWindowViewModel.cs) и кнопку в сайдбар.
6. Обновить [`json/viewmodels.json`](json/viewmodels.json) и [`json/relationships.json`](json/relationships.json).
7. Описать экран в [`07-viewmodels.md`](07-viewmodels.md) и [`08-views.md`](08-views.md).
8. Прогнать `dotnet build` и `dotnet test`.

Подробнее: [`PRINCIPLES.md`](PRINCIPLES.md) → раздел «Чек-лист добавления нового функционала».

---

## Чек-лист: добавить новую сущность

1. Создать класс сущности в [`AutoKassa/Models/`](../../AutoKassa/Models/).
2. Добавить `DbSet<T>` в [`AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs).
3. Настроить связи, индексы и ограничения в `OnModelCreating`.
4. Создать миграцию: `cd AutoKassa && dotnet ef migrations add <Name>`.
5. Обновить [`json/models.json`](json/models.json).
6. Описать сущность в [`04-models.md`](04-models.md) и [`03-database.md`](03-database.md).
7. При необходимости добавить сервис и VM для работы с сущностью.
8. Прогнать `dotnet build` и `dotnet test`.

---

## Чек-лист: добавить новый сервис

1. Создать интерфейс `INewService` и реализацию `NewService` в [`AutoKassa/Services/`](../../AutoKassa/Services/).
2. Зарегистрировать в DI с правильным lifetime в [`App.xaml.cs`](../../AutoKassa/App.xaml.cs).
3. Внедрить зависимости через конструктор, не использовать `App.GetService<T>()`.
4. Все операции с БД — асинхронные, с `CancellationToken` где уместно.
5. Обновить [`json/services.json`](json/services.json) и [`json/relationships.json`](json/relationships.json).
6. Описать сервис в [`05-services.md`](05-services.md).
7. Добавить тесты в [`AutoKassa.Tests/`](../../AutoKassa.Tests/).
8. Прогнать `dotnet build` и `dotnet test`.

---

## Главные правила за 30 секунд

- **MVVM:** бизнес-логика только в сервисах, ViewModel не знает о View.
- **DI:** зависимости через конструктор, правильный lifetime.
- **БД:** только async/await, `IDbContextFactory<AppDbContext>` для Singleton-сервисов.
- **Soft delete:** операции и категории не удаляются физически.
- **Долг не деньги:** `PaymentType.Debt` не влияет на баланс.
- **После изменений:** `dotnet build` + `dotnet test` + обнови документацию.

---

## Где что искать

| Что ищешь | Куда смотреть |
|-----------|---------------|
| Настройка DI и запуск | [`AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs) |
| Главное окно и навигация | [`AutoKassa/MainWindow.xaml`](../../AutoKassa/MainWindow.xaml) |
| Базовый класс VM | [`AutoKassa/Helpers/ViewModelBase.cs`](../../AutoKassa/Helpers/ViewModelBase.cs) |
| Команды | [`AutoKassa/Helpers/RelayCommand.cs`](../../AutoKassa/Helpers/RelayCommand.cs) |
| DbContext и связи | [`AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs) |
| CRUD операций | [`AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) |
| Кредитные карты | [`AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) |
| Долги | [`AutoKassa/Services/DebtService.cs`](../../AutoKassa/Services/DebtService.cs) |
| Отчёты | [`AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs) |
| Настройки | [`AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) |
| Тестовая БД | [`AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs) |

---

> Последнее обновление: подэтапы 3.1–3.2 — Markdown-описания.
