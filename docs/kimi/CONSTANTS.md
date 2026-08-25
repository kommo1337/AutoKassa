# CONSTANTS — константы, лимиты и значения по умолчанию

> Правило: если вы добавляете новое магическое число или строку, сначала проверьте,
> можно ли использовать существующую константу, и обновите этот файл + [`json/constants.json`](json/constants.json).
>
> Все значения сверены с кодом 2026-07-30. Машиночитаемая версия — [`json/constants.json`](json/constants.json).
> Пути исходников указаны относительно каталога `AutoKassa/`.

---

## 1. AppSettings — значения по умолчанию

Дефолты заданы одновременно в трёх местах: инициализаторах свойств [`Models/AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs), методах `CreateDefaultSettings()` / `ResetToDefaultsAsync()` в [`Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) и seed-данных [`Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs). При добавлении поля обновляй все три (+ `SettingsExportData`).

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| AppSettingsId | 1 | AppSettings.cs, SettingsService.cs, AppDbContext.cs | Single-row таблица; вторую строку создавать запрещено ([TRAPS.md](TRAPS.md) §3.3) |
| PasswordHash | `""` (пусто) | AppDbContext.cs (seed) | Пустая строка = первый запуск → `InitialSetupView` |
| AutoLockTimeout | 10 (мин) | AppDbContext.cs, SettingsService.cs | 0 = отключено; в UI валидируется 1–60 |
| AutoLockEnabled | true | AppSettings.cs, AppDbContext.cs | |
| Theme | `"Light"` | AppDbContext.cs, SettingsService.cs | Хранится строкой, хотя есть enum `ThemeMode` — stringly-typed |
| DefaultPeriodFilter | `"Month"` | AppDbContext.cs, SettingsService.cs | Пресеты — см. `PeriodHelper` (Today/Week/Month/Quarter/Year) |
| ShowNotifications | true | AppSettings.cs, AppDbContext.cs | Toast-уведомления; ⚠ чекбокс удалён из SettingsView.xaml — поле остаётся в БД, редактируется только в коде |
| ShowOperationsInSidebar | false | AppSettings.cs, AppDbContext.cs | Кнопка «Операции» в боковом меню; ⚠ чекбокс удалён из SettingsView.xaml — поле остаётся в БД |
| DefaultPageSize | 20 | AppSettings.cs, AppDbContext.cs, SettingsService.cs | ⚠ TransactionsViewModel игнорирует настройку (`_pageSize = 100`) — см. §9 |
| ConfirmDelete | true | AppSettings.cs, AppDbContext.cs | ⚠ чекбокс удалён из SettingsView.xaml — поле остаётся в БД |
| AutoGenerateReports | false | AppSettings.cs, AppDbContext.cs | |
| BackupEnabled | false | AppDbContext.cs, SettingsService.cs | Автобэкап выключен по умолчанию |
| AutoBackupDays | 7 | AppSettings.cs, AppDbContext.cs, SettingsService.cs | Валидируется 1–365 |
| BackupFrequency | `"Weekly"` | AppDbContext.cs, SettingsService.cs | Daily/Weekly/Monthly, строкой |
| BackupKeepCount | 10 | AppDbContext.cs, SettingsService.cs | Валидируется 1–100; ротация — `CleanupOldBackups` |
| BackupPath | null | AppDbContext.cs | MaxLength(500); санитизируется `SanitizeBackupPath` |
| RequirePasswordOnStartup | true | AppSettings.cs, AppDbContext.cs | |
| PasswordExpireDays | 0 | AppSettings.cs, AppDbContext.cs | 0 = никогда; валидируется 0–365 |
| Language | `"ru-RU"` | AppSettings.cs, AppDbContext.cs | Мультиязычность не реализована |
| WindowWidth | 1200 | AppSettings.cs, AppDbContext.cs, SettingsService.cs | ⚠ Меньше `MinWidth=1280` из MainWindow.xaml — см. §9 |
| WindowHeight | 700 | AppSettings.cs, AppDbContext.cs, SettingsService.cs | ⚠ Меньше `MinHeight=720` — см. §9 |
| DefaultOperationType | 2 (Expense) | AppSettings.cs, SettingsService.cs, AppDbContext.cs | Хранится как int; enum `OperationType` |
| DefaultPaymentType | 1 (Cash) | AppSettings.cs, AppDbContext.cs, SettingsService.cs | Хранится как int; enum `PaymentType` |
| DefaultIncomeCategoryId | null | AppSettings.cs, AppDbContext.cs | Выбранную категорию нельзя удалить (TRAPS §2.5) |
| DefaultExpenseCategoryId | null | AppSettings.cs, AppDbContext.cs | Аналогично |
| InitialBalance | 0 | AppSettings.cs, AppDbContext.cs | decimal(18,2); только для наличных (TRAPS §2.20) |
| CreditCardLimit | 0 | AppSettings.cs, AppDbContext.cs | decimal(18,2); ≥ 0 |
| CreditCardCurrentDebt | 0 | AppSettings.cs, AppDbContext.cs | decimal(18,2); ≥ 0 |
| CreditCardInterestRate | 0 | AppSettings.cs, AppDbContext.cs | decimal(5,2); ≥ 0 |
| CreditCardPaymentDay | 10 | AppSettings.cs, AppDbContext.cs, SettingsService.cs | ⚠ Валидируется 1–31, а `StatementDay` карты — 1–28 — см. §9 |
| CreditCardMinimumPaymentPercent | 5 | AppSettings.cs, AppDbContext.cs, SettingsService.cs | decimal(5,2); 0–100 |

---

## 2. Валидация и лимиты

Серверная валидация настроек — [`Services/Settings/SettingsValidator.cs`](../../AutoKassa/Services/Settings/SettingsValidator.cs); валидация полей ввода — в соответствующих ViewModels.

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| AutoLockMinutes | 1–60 | SettingsValidator.cs | Минуты |
| DefaultPageSize | 10–100 | SettingsValidator.cs | Дефолт 20 внутри диапазона |
| AutoBackupDays | 1–365 | SettingsValidator.cs | Дни |
| PasswordExpireDays | 0–365 | SettingsValidator.cs | Дни |
| BackupKeepCount | 1–100 | SettingsValidator.cs | |
| CreditCardLimit / CurrentDebt / InterestRate | ≥ 0 | SettingsValidator.cs | Отрицательные запрещены |
| CreditCardPaymentDay | 1–31 | SettingsValidator.cs | ⚠ Строже, чем `StatementDay` (1–28) — см. §9 |
| CreditCardMinimumPaymentPercent | 0–100 | SettingsValidator.cs | |
| BackupPath | ≤ 500 символов | SettingsValidator.cs, AppSettings.cs | Совпадает с `MaxLength(500)` |
| MinPasswordLengthSetup | 6 | InitialSetupViewModel.cs, PasswordRecoveryViewModel.cs | Единый минимум длины пароля |
| MinPasswordLengthChange | 6 | ChangePasswordViewModel.cs | Совпадает с первичной настройкой (с 2026-08-21) |
| MinSecurityAnswerLength | 2 | InitialSetupViewModel.cs | Ответ на секретный вопрос |
| CustomSecurityQuestion | 5–200 | InitialSetupViewModel.cs | `MaxCustomQuestionLength = 200` = `MaxLength(200)` модели |
| MaxCategoryNameLength | 100 | CategoryItemViewModel.cs | |
| MaxCounterpartyNameLength | 150 | CounterpartyEditViewModel.cs | |
| MaxPhoneLength | 20 | CounterpartyEditViewModel.cs | |
| MaxNotesLength | 500 | CounterpartyEditViewModel.cs | |
| MaxDescriptionLength | 500 | TransactionEditViewModel.cs | Описание операции |
| MaxCreditCardNameLength | 100 | CreditCardEditViewModel.cs | |
| MaxBankNameLength | 100 | CreditCardEditViewModel.cs | |
| StatementDay | 1–28 | CreditCardEditViewModel.cs | `MaxDayInShortestMonth = 28` — день существует в любом месяце |

---

## 3. Пагинация и фильтрация

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| MaxDetailTransactions | 1000 | ReportService.cs | Лимит детального отчёта; при усечении отчёт помечается `TruncationNote` («Показаны первые 1000 из N») |
| MaxCategoryTransactions | 1000 | ReportService.cs | Лимит отчёта по категориям |
| MaxCardPurchases | 500 | CreditCardService.cs | Лимит истории покупок карты в `GetCardWithPurchasesAsync` (последние по дате) |
| OperationsPageSize | 100 | TransactionsViewModel.cs (`_pageSize`) | ⚠ Захардкожено; `AppSettings.DefaultPageSize` (20) игнорируется — см. §9 |
| DashboardRecentCount | 10 | TransactionService.cs (`GetRecentAsync`) | Значение по умолчанию параметра `count` |
| DashboardTake | 100 | Dashboard/DashboardService.cs | `Take = 100` в фильтре операций дашборда |
| DashboardRecentTake | 100 | Dashboard/DashboardTransactionCoordinator.cs | `recent.Take(100)` |

---

## 4. Seed-данные

Источник — [`Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs) (`SeedData`). Менять Id/Name системных категорий запрещено ([TRAPS.md](TRAPS.md) §5.4).

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| SeedDate | 2024-01-01 | AppDbContext.cs | Фиксированная дата `CreatedAt` для детерминированных миграций |
| SeedIncomeCategoryIds | 1–4 | AppDbContext.cs | 1 «Ремонт авто», 2 «Ремонт мото», 3 «Диагностика», 4 «Прочие доходы»; все `IsSystem = true` |
| SeedExpenseCategoryIds | 5–11 | AppDbContext.cs | 5 «Запчасти», 6 «Зарплата», 7 «Аренда», 8 «Коммунальные услуги», 9 «Маркетинг», 10 «Прочие расходы», 11 «Погашение кредита» |
| RepaymentCategoryId | 11 | AppDbContext.cs | Системная категория погашений кредита |
| RepaymentCategoryName | `"Погашение кредита"` | CreditCardOperationHelper.cs (const), CreditCardService.cs | `CreditCardService.RepaymentCategoryName = CreditCardOperationHelper.RepaymentCategoryName`; не переименовывать без миграции |
| SeedCategoryColors | 11 цветов | AppDbContext.cs | `#6366f1 #f59e0b #14b8a6 #94a3b8 #ec4899 #f97316 #8b5cf6 #06b6d4 #84cc16 #ef4444 #64748b` (Id 1–11) |

---

## 5. UI-константы (XAML)

Источники — [`App.xaml`](../../AutoKassa/App.xaml) (ресурсы) и [`MainWindow.xaml`](../../AutoKassa/MainWindow.xaml) (каркас окна). Полный каталог конвертеров — см. [`15-converters.md`](15-converters.md).

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| WindowWidth | 1366 | MainWindow.xaml | ⚠ Не совпадает с `AppSettings.WindowWidth` (1200) — см. §9 |
| WindowHeight | 768 | MainWindow.xaml | ⚠ Не совпадает с `AppSettings.WindowHeight` (700) |
| WindowMinWidth | 1280 | MainWindow.xaml | Больше дефолта настроек (1200) |
| WindowMinHeight | 720 | MainWindow.xaml | Больше дефолта настроек (700) |
| TitleBarHeight | 36 | MainWindow.xaml | Задано дважды: `WindowChrome.CaptionHeight` и `RowDefinition` |
| SidebarWidth | 76 | MainWindow.xaml | Задано дважды в `ColumnDefinition` |
| FontSizeSmall | 12 | App.xaml | Ресурс `FontSizeSmall` |
| FontSizeNormal | 14 | App.xaml | Ресурс `FontSizeNormal` |
| FontSizeLarge | 16 | App.xaml | Совпадает с `FontSizeH3` |
| FontSizeH1 / H2 / H3 | 24 / 20 / 16 | App.xaml | Заголовки |
| PrimaryBrush | `#2196F3` | App.xaml | Основной акцент |
| SuccessBrush / ErrorBrush / WarningBrush | `#4CAF50` / `#F44336` / `#FF9800` | App.xaml | Семантические цвета |
| AccentGreenBrush / AccentRedBrush | `#22c55e` / `#ef4444` | App.xaml | Доходы / расходы |
| CardCornerRadius | 14 | App.xaml | Карточки; мелкие контролы — 6–8, «пилюли» — 16–20 |
| CategoryPresetColors | 12 цветов | CategoryItemViewModel.cs (`PresetColors`) | Первые 10 = цветам seed-категорий + `#10b981`, `#3b82f6` |
| ToastLifetime | 4 с | Views/ToastOverlayView.xaml.cs | `TotalTicks=100` × `TickIntervalMs=40`; увеличено с ~1,75 с, чтобы кнопка «Отменить» (undo удаления) успевала быть нажатой |

---

## 6. Отчёты, дашборд и экспорт

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| DashboardEpoch | 2000-01-01 | Dashboard/DashboardService.cs | Начало периода «за всё время» для кассового баланса |
| TopCategoriesCount | 5 | Dashboard/DashboardService.cs | Топ-5 категорий расходов и доходов |
| ReportPalette | 15 цветов | ReportService.cs | Палитра диаграмм, циклически (`colorIndex % 15`) |
| PercentageDecimals | 1 | ReportService.cs | `Math.Round(percentage, 1)` |
| PdfPageSize | A4 | ExportService.cs | `PageSizes.A4` |
| PdfMargin | 30 | ExportService.cs | Отступы страницы (пункты) |
| PdfDefaultFontSize | 10 | ExportService.cs | Заголовок — 18, период — 12, суммы — 14, подписи — 11 |
| ExcelTitleFontSize | 16 | ExportService.cs | Заголовок отчёта в Excel |
| ExcelHeaderRow | 10 | ExportService.cs | Строка заголовка таблицы; строки 1–9 — шапка отчёта |
| ReportDebounceInterval | 50 мс | ViewModels/Reports/BaseReportViewModel.cs | Debounce обновления отчётов |
| CancelToastDuration | 5 с | TransactionEditViewModel.cs | Таймер скрытия toast отмены |
| QuestPdfLicense | Community | ExportService.cs | `QuestPDF.Settings.License = LicenseType.Community` |

---

## 7. Безопасность и блокировка

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| MinPasswordLengthSetup | 6 | InitialSetupViewModel.cs, PasswordRecoveryViewModel.cs | ⚠ См. §9 |
| MinPasswordLengthChange | 4 | ChangePasswordViewModel.cs | ⚠ См. §9 |
| DefaultAutoLockTimeout | 10 мин | AppDbContext.cs (seed), SettingsService.cs | Таймер — `DispatcherTimer` в LockService (минуты), отсчитывает **простой**: сбрасывается по вводу пользователя (`MainWindowViewModel.NotifyUserActivity`, троттлинг 15 с); `timeout <= 0` отключает. Закрытие экрана блокировки без пароля = завершение приложения |
| PasswordHashAlgorithm | BCrypt | PasswordService.cs | WorkFactor не задан — дефолт библиотеки (10 для BCrypt.Net-Next 4.2.0) |
| PasswordHashMaxLength | 255 | AppSettings.cs | Также для `SecurityAnswerHash` |
| BackupFileNamePattern | `AutoKassa_Backup_yyyyMMdd_HHmmss.db` | SettingsService.cs | Дата последнего бэкапа парсится из имени |
| BackupAllowedRoots | `%USERPROFILE%`, каталог приложения | SettingsService.cs (`SanitizeBackupPath`) | Защита от path traversal (TRAPS §3.8) |
| SecurityQuestionsCount | 4 + Custom | Models/Enums/SecurityQuestion.cs | MotherMaidenName, FirstPetName, FirstSchoolName, FavoriteDish |

Подробнее о безопасности — [`11-settings-security.md`](11-settings-security.md).

---

## 8. Логирование

Источник — [`App.xaml.cs`](../../AutoKassa/App.xaml.cs) (`OnStartup`). Как читать логи — см. [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md).

| Имя | Значение | Где задано | Комментарий |
|-----|----------|------------|-------------|
| LogPath | `logs/autokassa-.log` | App.xaml.cs | Rolling дописывает дату: `autokassa-yyyymmdd.log` |
| LogMinimumLevel | Information | App.xaml.cs | |
| LogRollingInterval | Day | App.xaml.cs | |
| LogRetainedFileCount | 30 | App.xaml.cs | ≈ 30 дней хранения |
| LogOutputTemplate | `{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}` | App.xaml.cs | Уровни: `[INF]`, `[WRN]`, `[ERR]`, `[FTL]` |

---

## 9. Найденные несогласованности

Эти расхождения существуют в коде на момент аудита (2026-07-30). Не «исправляй» их молча — сначала согласуй (см. [TRAPS.md](TRAPS.md) §5).

1. ~~**Требования к длине пароля.**~~ **Устранено 2026-08-21:** смена пароля (`ChangePasswordViewModel`) теперь требует ≥ 6 символов, как первичная настройка и восстановление. Единое правило: минимум 6 везде.
2. **День платежа по кредиту.** `AppSettings.CreditCardPaymentDay` валидируется диапазоном 1–31 (`SettingsValidator`), а `StatementDay` карты — 1–28 (`CreditCardEditViewModel`, константа `MaxDayInShortestMonth`). Разные поля, но концептуально один смысл.
3. **Размер страницы операций.** Настройка `AppSettings.DefaultPageSize = 20` (валидируется 10–100) не используется списком операций: `TransactionsViewModel._pageSize = 100` захардкожено.
4. **Размеры окна.** `AppSettings.WindowWidth/WindowHeight = 1200×700` не совпадают со стартовым размером `MainWindow.xaml` (1366×768) и даже меньше его минимума (1280×720).
5. **Дублирование дефолтов AppSettings.** Каждый дефолт повторён в трёх местах: инициализаторы свойств модели, `SettingsService` (`CreateDefaultSettings` + `ResetToDefaultsAsync`), seed в `AppDbContext`. При добавлении поля легко рассинхронизировать.
6. **Theme как строка.** `AppSettings.Theme` — строка `"Light"`, хотя существует enum `ThemeMode` (Light=0, Dark=1).
7. **Цвет категории Id=11.** У «Погашение кредита» в seed цвет `#64748b`, которого нет в `PresetColors` палитры (12 цветов).

---

## 10. Связанные документы

- [`json/constants.json`](json/constants.json) — машиночитаемая версия этого справочника.
- [`TRAPS.md`](TRAPS.md) — ловушки, связанные с константами (in-memory поиск, seed-ID, кеш настроек).
- [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) — диагностика проблем (логи, БД, экспорт).
- [`14-startup.md`](14-startup.md) — порядок инициализации (миграции → WAL → кеш настроек).
- [`15-converters.md`](15-converters.md) — конвертеры форматирования значений.
- [`INDEX.md`](INDEX.md) — полная карта документации.

> Последнее обновление: 2026-08-21 — устранена несогласованность длины пароля (§9 п.1): при смене пароля минимум 6 символов, как при первичной настройке и восстановлении. Ранее: 2026-08-09 — отмечены удалённые из UI настройки (`ShowNotifications`, `ShowOperationsInSidebar`, `ConfirmDelete`), обновлён механизм автоблокировки (по простою, закрытие экрана = выход), добавлено время жизни toast (4 с). Этап 7 — создан справочник констант (аудит кода 2026-07-30).
