# AutoKassa — Руководство для AI-агентов

> Этот файл предназначен для AI-агентов, работающих с кодовой базой. Читатель не знает о проекте ничего.

---

## 1. Обзор проекта

**AutoKassa** — десктопное WPF-приложение для учёта финансовых операций автосервиса (доходы/расходы). Приложение работает локально, без облачных сервисов, хранит данные в SQLite.

**Основной функционал:**
- Учёт финансовых операций (доходы/расходы) с категориями
- Разделение по типу оплаты: наличные / безналичные
- Дашборд со сводкой, графиками и последними операциями
- Управление категориями (CRUD, сортировка, цвета)
- Отчёты (баланс, структура по категориям, детализация операций)
- Экспорт отчётов в PDF (QuestPDF) и Excel (ClosedXML)
- Настройки приложения с экспортом/импортом в JSON
- Резервное копирование базы данных
- Защита паролем с автоблокировкой экрана
- Toast-уведомления

**Язык интерфейса и документации:** русский. Все комментарии, XML-документация и UI-тексты на русском языке.

---

## 2. Технологический стек

| Компонент | Технология | Версия |
|-----------|-----------|--------|
| Платформа | .NET | 10.0-windows |
| UI-фреймворк | WPF | — |
| ORM | Entity Framework Core | 10.0.8 |
| База данных | SQLite | — |
| DI-контейнер | Microsoft.Extensions.DependencyInjection | 10.0.8 |
| Графики | OxyPlot.Wpf | 2.2.0 |
| PDF-экспорт | QuestPDF | 2026.5.0 |
| Excel-экспорт | ClosedXML | 0.105.0 |
| Хеширование паролей | BCrypt.Net-Next | 4.2.0 |
| Логирование | Serilog + File sink | 4.3.1 / 7.0.0 |
| Тестирование | xUnit + FluentAssertions | 2.9.3 / 8.10.0 |

---

## 3. Структура проекта

```
AutoKassa/
├── App.xaml / App.xaml.cs          # Точка входа, DI-контейнер, глобальные стили
├── MainWindow.xaml / .xaml.cs      # Главное окно (сайдбар + контентная область)
│
├── Models/                          # Сущности БД и DTO
│   ├── Enums/
│   │   ├── OperationType.cs        # Income=1, Expense=2
│   │   ├── PaymentType.cs          # Cash=1, NonCash=2, CreditCard=3
│   │   └── SecurityQuestion.cs     # Секретные вопросы для восстановления пароля
│   ├── Reports/
│   │   ├── BalanceReport.cs
│   │   ├── CategoryReport.cs
│   │   └── TransactionDetailReport.cs
│   ├── Transaction.cs
│   ├── Category.cs
│   ├── CreditCard.cs               # Кредитная карта
│   ├── CreditCardPurchase.cs       # Покупка по кредитной карте
│   ├── AppSettings.cs              # Single-row настройки (Id=1)
│   ├── FavoriteReport.cs
│   ├── FilterParameters.cs         # TransactionFilterParameters
│   ├── DailyTotalsItem.cs
│   └── ReportBase.cs
│
├── Services/                        # Бизнес-логика и доступ к данным
│   ├── AppDbContext.cs             # EF Core DbContext + seed data
│   ├── I*Service.cs / *Service.cs  # Интерфейс + реализация для каждого сервиса
│   └── ToastItem.cs
│
├── ViewModels/                      # MVVM ViewModels
│   ├── Reports/
│   │   ├── BaseReportViewModel.cs
│   │   ├── BalanceReportViewModel.cs
│   │   ├── CategoryReportViewModel.cs
│   │   └── TransactionDetailReportViewModel.cs
│   └── <Screen>ViewModel.cs
│
├── Views/                           # XAML-представления
│   ├── Reports/
│   │   ├── BalanceReportView.xaml
│   │   ├── CategoryReportView.xaml
│   │   └── TransactionDetailReportView.xaml
│   └── <Screen>View.xaml
│
├── Helpers/                         # Вспомогательные классы
│   ├── ViewModelBase.cs            # INPC + INotifyDataErrorInfo + IDisposable
│   ├── RelayCommand.cs             # ICommand реализация
│   ├── PeriodHelper.cs
│   ├── BlurHelper.cs
│   ├── DisplayItem.cs
│   ├── DonutSliceItem.cs
│   ├── SecurityQuestionHelper.cs
│   ├── TransactionDetailGroup.cs
│   └── Converters/                 # 15+ XAML-конвертеров
│
└── Migrations/                      # EF Core миграции

AutoKassa.Tests/                     # Юнит-тесты
├── Infrastructure/
│   └── TestDatabase.cs             # Фабрика in-memory SQLite для тестов
└── Services/
    ├── TransactionServiceTests.cs
    ├── CategoryServiceTests.cs
    ├── SettingsServiceTests.cs
    ├── PasswordServiceTests.cs
    └── ReportServiceTests.cs
```

---

## 4. Архитектура

### 4.1. MVVM

- **Model** — сущности EF Core (`Transaction`, `Category`, `AppSettings`)
- **ViewModel** — бизнес-логика экрана, команды, свойства для биндинга. Все VM наследуют `ViewModelBase`
- **View** — XAML-разметка. Code-behind содержит ТОЛЬКО минимальную UI-логику (например, обработчики кнопок тайтлбара). Никакой бизнес-логики в code-behind
- Связь через `DataBinding`, `ICommand` (`RelayCommand`), `INotifyPropertyChanged`

### 4.2. Dependency Injection

Вся регистрация в `App.xaml.cs` → `ConfigureServices(IServiceCollection)`:

- **Singleton:** `NavigationService`, `PasswordService`, `SettingsService`, `DialogService`, `ToastNotificationService`, `LockService`, `ExportService`
- **Scoped:** `TransactionService`, `CategoryService`, `ReportService`
- **Transient:** все ViewModels
- **DbContext:** `AddDbContext<AppDbContext>()` + `AddDbContextFactory<AppDbContext>()`

Статический доступ к контейнеру: `App.GetService<T>()`

### 4.3. Навигация

`INavigationService` управляет текущим VM. `MainWindow.xaml` содержит `ContentControl` с `DataTemplate` для каждого VM → View.

Навигационные экраны: Dashboard → Операции → Отчёты → Настройки. Сайдбар — тёмная панель слева шириной 76px с иконками.

### 4.4. База данных

- **Провайдер:** SQLite, файл `AutoKassa.db` в папке приложения
- **Seed-данные:** 10 системных категорий (4 дохода + 6 расходов) + дефолтные `AppSettings` создаются через `OnModelCreating`
- **Soft delete:** у `Transaction` есть `IsDeleted`. Удалённые записи фильтруются на уровне сервиса
- **Миграции:** применяются автоматически при старте в `App.OnStartup()` через `SettingsService.MigrateAsync()`

### 4.5. Логирование

Serilog с записью в файл `logs/autokassa-.log` (rolling по дням, хранение 30 дней). Логируются все необработанные исключения (Dispatcher, AppDomain, TaskScheduler).

---

## 5. Ключевые сущности

### Transaction (операция)
| Поле | Описание |
|------|----------|
| Id | PK |
| Date | Дата операции |
| Amount | Сумма (decimal 18,2) |
| Type | OperationType (Income/Expense) |
| PaymentType | PaymentType (Cash/NonCash/CreditCard), default = Cash |
| CategoryId | FK → Category |
| CreditCardId | FK → CreditCard (nullable, для кредитных покупок) |
| Description | Описание, max 500 |
| CreatedAt / UpdatedAt | Даты создания/изменения |
| IsDeleted | Soft delete flag |

### CreditCard (кредитная карта)
| Поле | Описание |
|------|----------|
| Id | PK |
| Name | Название карты |
| BankName | Банк-эмитент |
| Limit | Кредитный лимит |
| InterestRate | Годовая ставка, % |
| StatementDay | День выписки (1–31) |
| PaymentDay | День платежа (1–31) |
| LastPaymentDate | Дата последнего платежа |
| MinimumPaymentPercent | Процент от долга для мин. платежа |
| InitialDebt | Начальный долг из настроек |
| IsActive | Активна ли карта |
| CreatedAt | Дата создания |

### CreditCardPurchase (покупка по кредитной карте)
| Поле | Описание |
|------|----------|
| Id | PK |
| CreditCardId | FK → CreditCard |
| TransactionId | FK → Transaction |
| Amount | Сумма покупки |
| RemainingDebt | Оставшийся долг по покупке |
| PurchaseDate | Дата покупки |
| Notes | Примечание |

### Category (категория)
| Поле | Описание |
|------|----------|
| Id | PK |
| Name | Название, unique в рамках Type |
| Type | OperationType |
| IsActive | Активность (вместо удаления можно деактивировать) |
| IsSystem | Системная категория (нельзя удалить) |
| Color | HEX-цвет, например `#6366f1` |
| SortOrder | Порядок сортировки |

### AppSettings (настройки, single-row)
| Поле | Описание |
|------|----------|
| Id | Всегда 1 |
| PasswordHash | BCrypt-хеш пароля |
| SecurityQuestionId / SecurityAnswerHash | Восстановление пароля |
| AutoLockTimeout / AutoLockEnabled | Автоблокировка |
| Theme | Light / Dark |
| BackupEnabled / BackupPath / AutoBackupDays | Резервное копирование |
| ShowOperationsInSidebar | Показывать ли кнопку "Операции" в сайдбаре |
| DefaultPageSize / ConfirmDelete | Настройки операций |
| InitialBalance / DefaultPaymentType | Финансовые настройки |
| WindowWidth / WindowHeight / Language | Интерфейс |

---

## 6. Сборка и запуск

```bash
# Сборка всего решения
dotnet build

# Запуск приложения
dotnet run --project AutoKassa

# Запуск тестов
dotnet test

# Добавление NuGet-пакета
dotnet add package PackageName

# Восстановление зависимостей
dotnet restore
```

---

## 7. Миграции базы данных

```bash
# Создание миграции (выполнять из папки AutoKassa/)
dotnet ef migrations add MigrationName

# Применение миграций (или через Update-Database в PMC)
dotnet ef database update
```

Миграции применяются автоматически при старте приложения. Вручную запускать обычно не требуется, кроме случаев разработки новых миграций.

---

## 8. Стиль кодирования

### 8.1. Именование
- **PascalCase** — классы, методы, свойства, константы
- **camelCase** — параметры методов, локальные переменные
- **_camelCase** — private поля
- **Async** суффикс — для асинхронных методов (`GetTransactionsAsync`)

### 8.2. Комментарии
- XML-комментарии (`/// <summary>`) **обязательны** для всего public API
- Inline-комментарии — для сложной/нетривиальной логики
- Комментарии на русском языке

### 8.3. Async/Await
- Все операции с БД — **строго асинхронные** (`async Task` + `await`)
- Используйте `CancellationToken` в публичных методах сервисов, где это имеет смысл

### 8.4. MVVM-правила
- **Никакой бизнес-логики в code-behind View**
- ViewModel не должен знать о View
- Используйте `RelayCommand` и `RelayCommand<T>` для команд
- Для свойств используйте `SetProperty<T>` из `ViewModelBase`
- Валидация через `INotifyDataErrorInfo` (`SetErrors` / `ClearErrors`)

### 8.5. Работа с БД
- Сервисы получают `AppDbContext` через конструктор (DI)
- `SettingsService` использует `IDbContextFactory<AppDbContext>` для создания контекстов вручную (кеширование настроек)
- Seed-данные добавляются только в `AppDbContext.OnModelCreating`

### 8.6. Логирование
- Каждый сервис имеет статический `ILogger` через `Log.ForContext<T>()`
- Логируйте значимые события: создание, обновление, удаление операций/категорий
- Ошибки БД и файловой системы — всегда логируйте с полным исключением

---

## 9. Тестирование

### 9.1. Инфраструктура
- Тестовая база — SQLite **in-memory** (`DataSource=:memory:`)
- `TestDatabase.Create()` возвращает `(AppDbContext context, SqliteConnection connection)`
- Соединение должно оставаться открытым на время теста — вызывайте `connection.Dispose()` в `Dispose()` тест-класса
- Seed-данные (категории) создаются автоматически через `EnsureCreated()`

### 9.2. Хелперы для тестов
```csharp
// Создать категории
TestDatabase.SeedExpenseCategory(ctx, "Имя");
TestDatabase.SeedIncomeCategory(ctx, "Имя");

// Создать транзакцию
TestDatabase.SeedTransaction(ctx, categoryId, amount,
    OperationType.Expense, PaymentType.Cash, date, description);
```

### 9.3. Запуск тестов
```bash
dotnet test
```

### 9.4. Что покрывается тестами
- CRUD операций (`TransactionServiceTests`)
- Фильтрация и пагинация
- SQL-агрегации (итоги за период, дневные группировки)
- Soft delete / restore
- `CancellationToken` propagation

---

## 10. Безопасность

- **Пароли** хешируются через `BCrypt` (`PasswordService`)
- **Восстановление пароля** — через секретный вопрос + ответ (также BCrypt)
- **Автоблокировка** — по таймауту неактивности, реализована через `DispatcherTimer` в `LockService`
- **Backup path sanitization** — `SettingsService.SanitizeBackupPath()` предотвращает path traversal при импорте настроек
- **Резервное копирование БД** — используется SQLite Online Backup API (`SqliteConnection.BackupDatabase`), без SQL-инъекций и без закрытия соединений
- **Логи** не содержат паролей или хешей

---

## 11. Важные конвенции проекта

### 11.1. Софт-делит
Удаление операций — только soft delete (`IsDeleted = true`). Физическое удаление из БД запрещено. Удалённые операции можно восстановить (`RestoreAsync`).

### 11.2. Категории
- Нельзя удалить категорию, если она установлена по умолчанию в настройках
- Нельзя удалить категорию с привязанными операциями (даже удалёнными)
- Системные категории (`IsSystem = true`) нельзя удалить
- Уникальность названия категории — в рамках типа (доход/расход)
- Добавлена системная категория расходов «Погашение кредита» (Id = 11)

### 11.3. Поиск по описанию
SQLite `LOWER()` не поддерживает Unicode (кириллицу). Поиск по `SearchText` выполняется в памяти после SQL-фильтрации. Это сознательный компромисс.

### 11.4. Стили и UI
- Глобальные стили определены в `App.xaml` (цвета, конвертеры, стили кнопок, DataGrid, ComboBox, DatePicker/Calendar)
- Кастомный DatePicker с полным переопределением шаблона (убрана стандартная иконка календаря)
- Главное окно — borderless с кастомным тайтлбаром через `WindowChrome`
- Сайдбар — тёмная панель `#1a1d23` слева, ширина 76px

### 11.5. Добавление нового экрана
Чтобы добавить новый экран:
1. Создать ViewModel, наследующий `ViewModelBase`
2. Создать View (UserControl/XAML)
3. Зарегистрировать VM в DI: `services.AddTransient<NewViewModel>()` в `App.xaml.cs`
4. Добавить `DataTemplate` в `MainWindow.xaml` в ресурсы `ContentControl`
5. Добавить команду навигации в `MainWindowViewModel`
6. При необходимости — добавить кнопку в сайдбар `MainWindow.xaml`

---

## 12. Полезные команды для агентов

```bash
# Быстрая проверка сборки
dotnet build AutoKassa.sln

# Запуск только тестов сервисов
dotnet test --filter "FullyQualifiedName~Services"

# Создать миграцию
cd AutoKassa && dotnet ef migrations add <ИмяМиграции>

# Просмотр структуры БД (если установлен sqlite3)
sqlite3 AutoKassa/bin/Debug/net10.0-windows/AutoKassa.db ".schema"
```

---

## 13. Известные особенности

- `AutoKassa.csproj` использует `<UseWPF>true</UseWPF>` и `<OutputType>WinExe</OutputType>` — это Windows-only приложение
- Приложение требует первоначальной настройки пароля при первом запуске (`InitialSetupView`)
- `AppSettings` — single-row таблица, всегда `Id = 1`
- `DefaultOperationType` хранится как `int` в БД, но интерфейс работает с `OperationType` enum
- `SettingsService` кеширует настройки в памяти (`_cachedSettings`) для синхронных операций
