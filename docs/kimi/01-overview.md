# 01-overview — общее описание проекта AutoKassa

---

## Назначение

AutoKassa — десктопное WPF-приложение для учёта финансовых операций автосервиса. Предназначено для локальной работы на одном ПК без облачных сервисов. Все данные хранятся в SQLite-базе рядом с исполняемым файлом.

**Целевая аудитория:** владельцы и бухгалтеры небольших автосервисов, которым нужен простой инструмент для учёта доходов, расходов, долгов и кредитных карт.

---

## Основной функционал

| Функция | Описание | Файлы |
|---------|----------|-------|
| Учёт операций | Доходы и расходы с категориями, суммой, датой, описанием и типом оплаты. | [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs), [`TransactionEditViewModel.cs`](../../AutoKassa/ViewModels/TransactionEditViewModel.cs) |
| Типы оплаты | Наличные, безналичные, кредитная карта, долг. | [`PaymentType.cs`](../../AutoKassa/Models/Enums/PaymentType.cs) |
| Дашборд | Сводка за период, график, последние операции, быстрое добавление. | [`DashboardViewModel.cs`](../../AutoKassa/ViewModels/DashboardViewModel.cs), [`DashboardService.cs`](../../AutoKassa/Services/Dashboard/DashboardService.cs) |
| Справочник категорий | CRUD категорий доходов/расходов, сортировка, цвета, системные категории. | [`CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs), [`CategoriesViewModel.cs`](../../AutoKassa/ViewModels/CategoriesViewModel.cs) |
| Справочник контрагентов | Клиенты, поставщики, филиалы, прочие. | [`CounterpartyService.cs`](../../AutoKassa/Services/CounterpartyService.cs) |
| Кредитные карты | Карты, покупки, FIFO-погашение, минимальный платёж, доступный лимит. | [`CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) |
| Долги | Дебиторская/кредиторская задолженность, частичные погашения, списание. | [`DebtService.cs`](../../AutoKassa/Services/DebtService.cs) |
| Отчёты | Баланс, структура по категориям, детализация операций, отчёт по долгам. | [`ReportService.cs`](../../AutoKassa/Services/ReportService.cs), [`ViewModels/Reports/`](../../AutoKassa/ViewModels/Reports/) |
| Экспорт | PDF через QuestPDF, Excel через ClosedXML. | [`ExportService.cs`](../../AutoKassa/Services/ExportService.cs) |
| Настройки | Пароль, тема, начальный баланс, категории по умолчанию, путь бэкапа. | [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs), [`AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs) |
| Безопасность | BCrypt-хеширование пароля, автоблокировка, восстановление через секретный вопрос. | [`PasswordService.cs`](../../AutoKassa/Services/PasswordService.cs), [`LockService.cs`](../../AutoKassa/Services/LockService.cs) |
| Резервное копирование | SQLite Online Backup API, авто-бэкап по расписанию. | [`BackupCoordinator.cs`](../../AutoKassa/Services/Backup/BackupCoordinator.cs) |
| Тестирование | Юнит-тесты на in-memory SQLite (xUnit + FluentAssertions). | [`AutoKassa.Tests/`](../../AutoKassa.Tests/) |

---

## Технологический стек

Подробный стек с версиями NuGet см. в [`json/metadata.json`](json/metadata.json).

| Слой | Технология |
|------|------------|
| Платформа | .NET 10.0-windows |
| UI-фреймворк | WPF |
| ORM | Entity Framework Core 10.0.8 |
| База данных | SQLite (SourceGear.sqlite3) |
| DI-контейнер | Microsoft.Extensions.DependencyInjection 10.0.8 |
| Графики | OxyPlot.Wpf 2.2.0 |
| PDF-экспорт | QuestPDF 2026.5.0 |
| Excel-экспорт | ClosedXML 0.105.0 |
| Хеширование | BCrypt.Net-Next 4.2.0 |
| Логирование | Serilog 4.3.1 + File sink 7.0.0 |
| Тестирование | xUnit 2.9.3 + FluentAssertions 8.10.0 |

---

## Структура решения

```
AutoKassa/
├── App.xaml / App.xaml.cs          # Точка входа, DI, глобальные стили
├── MainWindow.xaml / .xaml.cs      # Главное окно, сайдбар, DataTemplates
├── Models/                          # Сущности EF Core и enum
│   ├── Transaction.cs
│   ├── Category.cs
│   ├── Counterparty.cs
│   ├── CreditCard.cs
│   ├── CreditCardPurchase.cs
│   ├── CreditCardPayment.cs
│   ├── DebtPayment.cs
│   ├── AppSettings.cs
│   ├── FavoriteReport.cs
│   └── Enums/
├── Services/                        # Бизнес-логика и доступ к данным
│   ├── AppDbContext.cs
│   ├── TransactionService.cs
│   ├── CategoryService.cs
│   ├── CounterpartyService.cs
│   ├── DebtService.cs
│   ├── CreditCardService.cs
│   ├── ReportService.cs
│   ├── ExportService.cs
│   ├── SettingsService.cs
│   ├── NavigationService.cs
│   ├── DialogService.cs
│   ├── ToastNotificationService.cs
│   ├── LockService.cs
│   ├── PasswordService.cs
│   ├── IDataChangeService.cs
│   ├── Backup/
│   ├── CreditCards/
│   ├── Dashboard/
│   ├── Settings/
│   └── Transactions/
├── ViewModels/                      # MVVM ViewModels
│   ├── *ViewModel.cs
│   └── Reports/
├── Views/                           # XAML-представления
│   ├── *View.xaml
│   └── Reports/
├── Helpers/                         # ViewModelBase, RelayCommand, конвертеры
└── Migrations/                      # EF Core миграции

AutoKassa.Tests/
├── Infrastructure/TestDatabase.cs
└── Services/*Tests.cs
```

---

## Ключевые архитектурные решения

- **MVVM:** ViewModel не зависит от View, бизнес-логика в сервисах.
- **DI:** все зависимости через конструктор, регистрация в [`App.xaml.cs`](../../AutoKassa/App.xaml.cs).
- **Async/await:** все операции с БД асинхронные.
- **Soft delete:** операции не удаляются физически, у них устанавливается `IsDeleted = true`.
- **Single-row настройки:** таблица `AppSettings` всегда содержит одну строку с `Id = 1`.
- **Долг ≠ деньги:** `PaymentType.Debt` фиксирует обязательство, но не влияет на баланс.
- **Кредитные карты:** покупки хранятся отдельно, погашение распределяется FIFO по `CreditCardPurchase.RemainingDebt`.

Подробнее: [`02-architecture.md`](02-architecture.md), [`06-business-logic.md`](06-business-logic.md).

---

## Связь с другими документами

| Документ | Отличие от `01-overview.md` |
|----------|-----------------------------|
| [`AGENTS.md`](../../AGENTS.md) | Базовое руководство для AI-агентов: стек, структура, стиль кодирования, безопасность. |
| [`README.md`](../../README.md) | Пользовательская документация: установка, скриншоты, быстрый старт для человека. |
| [`kimi.md`](kimi.md) | Точка входа для KIMI: правила, карта «Я хочу…», ссылки на справочники. |
| [`00-quickstart.md`](00-quickstart.md) | Короткий обзор и чек-листы для нового функционала. |
| [`json/metadata.json`](json/metadata.json) | Машиночитаемые метаданные: версии, пути, проекты. |

---

## Статистика кодовой базы

- Проектов в решении: 2 (`AutoKassa`, `AutoKassa.Tests`).
- EF-сущностей: 9.
- Сервисов: 29.
- ViewModels: 36.
- Главных экранов: 8.
- Enum: 9.
- Миграций: 14 (см. [`json/models.json`](json/models.json) → `migrations`).

---

> Последнее обновление: подэтапы 3.1–3.2 — Markdown-описания.
