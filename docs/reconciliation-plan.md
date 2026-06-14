# План реализации окна «Сверка кассы»

## 1. Цель окна

Информационное окно для менеджера, в котором собраны все суммы, необходимые для вечерней сверки кассы. Менеджер не вводит данные — только сверяет цифры из приложения с фактическими остатками.

---

## 2. Что показывать

| Показатель | Источник |
|------------|----------|
| Наличные на дату | `TransactionService.GetPeriodTotalsAsync(date, date, PaymentType.Cash)` |
| Безналичные на дату | `TransactionService.GetPeriodTotalsAsync(date, date, PaymentType.NonCash)` |
| Кредитный долг | `CreditCardService.GetTotalDebtAsync()` |
| Ближайший платёж | `CreditCardService.GetNextPaymentDateAsync` + `GetMinimumPaymentAsync` |
| Фактический остаток | Наличные + Безналичные |
| Чистый баланс | Фактический остаток − Кредитный долг |

---

## 3. Общие требования к UI

Окно должно строго придерживаться существующего дизайна приложения:

- **Фон страницы**: `{StaticResource BackgroundBrush}`
- **Фон карточек**: `{StaticResource CardBgBrush}`
- **Заголовки/подписи**: `{StaticResource MutedTextBrush}`
- **Основной текст**: `{StaticResource TextPrimaryBrush}`
- **Положительные суммы**: `{StaticResource AccentGreenBrush}`
- **Отрицательные суммы/долги**: `{StaticResource AccentRedBrush}`
- **Кредит/фиолетовый акцент**: `#6366f1`
- **Скругление карточек**: `CornerRadius="12"`
- **Тень карточек**: стандартный `DropShadowEffect` с `Opacity="0.06"`, `BlurRadius="10"`, `ShadowDepth="1"`, `Direction="270"`
- **Шрифт**: системный шрифт приложения (наследуется автоматически)
- **Отступы**: `Margin="20"` для контента карточек, `Margin="0,0,12,0"` между карточками

---

## 4. Готовые UI-элементы и ресурсы

### 4.1. Стили и кисти (App.xaml)

| Ресурс | Назначение |
|--------|------------|
| `{StaticResource BackgroundBrush}` | Фон всего окна |
| `{StaticResource CardBgBrush}` | Фон карточек (белый) |
| `{StaticResource BorderLightBrush}` | Разделители, бордеры |
| `{StaticResource TextPrimaryBrush}` | Основной тёмный текст |
| `{StaticResource MutedTextBrush}` | Подписи, второстепенный текст |
| `{StaticResource AccentGreenBrush}` | Доходы, положительные суммы |
| `{StaticResource AccentRedBrush}` | Расходы, долги, отрицательные суммы |
| `{StaticResource PeriodPresetButtonStyle}` | Стиль кнопок быстрого выбора периода |

### 4.2. Конвертеры

| Конвертер | Где находится | Назначение |
|-----------|---------------|------------|
| `DecimalToStringConverter` | `Helpers/Converters/DecimalToStringConverter.cs` | Форматирование `decimal` в строку с ₽ |
| `IsNegativeConverter` | `Helpers/Converters/IsNegativeConverter.cs` | Определение отрицательного числа |
| `BoolToVisibilityConverter` | `Helpers/Converters/BoolToVisibilityConverter.cs` | Скрытие/показ элементов |

### 4.3. Готовые элементы управления

| Элемент | Где взять пример | Примечание |
|---------|------------------|------------|
| `DatePicker` | `BalanceReportView.xaml`, `TransactionEditView.xaml` | Использовать с шириной `Width="130"` |
| Кнопка-период («Сегодня») | `BalanceReportView.xaml` | Стиль `{StaticResource PeriodPresetButtonStyle}` |
| Карточка с тенью | `DashboardView.xaml`, `BalanceReportView.xaml` | `Border` + `DropShadowEffect` |
| Карточка с цветным фоном | `BalanceReportView.xaml` (после доработки) | Сплошные цвета `#F0FFF4`, `#F5F3FF`, `#F8FAFC` |
| `ToolTip` | `BalanceReportView.xaml` | Подсказки при наведении |

---

## 5. Структура экрана

### 5.1. Верхняя панель

```xml
<Grid>
    <TextBlock Text="Сверка кассы" FontSize="20" FontWeight="SemiBold" Foreground="{StaticResource TextPrimaryBrush}"/>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
        <DatePicker SelectedDate="{Binding ReconciliationDate}" Width="130" Margin="0,0,8,0"/>
        <Button Content="Сегодня" Command="{Binding SetTodayCommand}" Style="{StaticResource PeriodPresetButtonStyle}"/>
    </StackPanel>
</Grid>
```

### 5.2. Карточки

#### Карточка «Наличные»

- **Фон**: `{StaticResource CardBgBrush}`
- **Внутренний фон акцента**: `#F0FFF4`
- **Заголовок**: «Наличные», `FontSize="14"`, `FontWeight="SemiBold"`
- **Сумма**: `FontSize="22"`, `FontWeight="Bold"`, `Foreground="{StaticResource TextPrimaryBrush}"`
- **Подсказка**: «Пересчитайте наличные деньги в кассе», `FontSize="12"`, `Foreground="{StaticResource MutedTextBrush}"`
- **ToolTip**: «Сумма наличных операций за выбранный день»

#### Карточка «Безналичные»

- **Фон**: `{StaticResource CardBgBrush}`
- **Внутренний фон акцента**: `#EFF6FF`
- **Заголовок**: «Безналичные», `FontSize="14"`, `FontWeight="SemiBold"`
- **Сумма**: `FontSize="22"`, `FontWeight="Bold"`, `Foreground="{StaticResource TextPrimaryBrush}"`
- **Подсказка**: «Сверьте с выпиской банка / терминала», `FontSize="12"`, `Foreground="{StaticResource MutedTextBrush}"`
- **ToolTip**: «Сумма безналичных операций за выбранный день»

#### Карточка «Кредитный долг»

- **Фон**: `{StaticResource CardBgBrush}`
- **Внутренний фон акцента**: `#F5F3FF`
- **Заголовок**: «Кредитный долг», `FontSize="14"`, `FontWeight="SemiBold"`
- **Сумма долга**: `FontSize="22"`, `FontWeight="Bold"`, `Foreground="{StaticResource AccentRedBrush}"`
- **Ближайший платёж**: `FontSize="13"`, `Foreground="{StaticResource MutedTextBrush}"`
- **Подсказка**: «Проверьте покупки по кредитным картам», `FontSize="12"`, `Foreground="{StaticResource MutedTextBrush}"`
- **ToolTip**: «Общий текущий долг по всем кредитным картам»

#### Карточка «Фактический остаток»

- **Фон**: `{StaticResource CardBgBrush}`
- **Внутренний фон акцента**: `#F8FAFC`
- **Заголовок**: «Фактический остаток», `FontSize="14"`, `FontWeight="SemiBold"`
- **Сумма**: `FontSize="22"`, `FontWeight="Bold"`, `Foreground="{StaticResource TextPrimaryBrush}"`
- **Формула**: «Наличные + Безналичные», `FontSize="12"`, `Foreground="{StaticResource MutedTextBrush}"`
- **ToolTip**: «Реальные деньги без учёта кредита»

#### Карточка «Чистый баланс»

- **Фон**: `{StaticResource CardBgBrush}`
- **Внутренний фон акцента**: `#F0FDF4` при положительном, `#FFF5F5` при отрицательном
- **Заголовок**: «Чистый баланс», `FontSize="14"`, `FontWeight="SemiBold"`
- **Сумма**: `FontSize="26"`, `FontWeight="Bold"`
- **Цвет суммы**: зелёный если ≥ 0, красный если < 0 (через `IsNegativeConverter`)
- **Формула**: «Фактический остаток − Кредитный долг», `FontSize="12"`, `Foreground="{StaticResource MutedTextBrush}"`
- **ToolTip**: «Деньги, которые останутся после погашения всего кредитного долга»

---

## 6. ViewModel

### Файл

`AutoKassa/ViewModels/ReconciliationViewModel.cs`

### Свойства

```csharp
public DateTime ReconciliationDate { get; set; }
public decimal CashAmount { get; set; }
public decimal NonCashAmount { get; set; }
public decimal CreditDebt { get; set; }
public decimal NextPaymentAmount { get; set; }
public DateTime? NextPaymentDate { get; set; }
public decimal FactBalance { get; set; }
public decimal NetBalance { get; set; }
public string NetBalanceFormula { get; set; }
```

### Команды

```csharp
public ICommand SetTodayCommand { get; }
```

### Логика

- При изменении `ReconciliationDate` перезагружать данные через `IReportService`.
- `FactBalance = CashAmount + NonCashAmount`
- `NetBalance = FactBalance - CreditDebt`
- `NetBalanceFormula = $"{FactBalance:N2} ₽ − {CreditDebt:N2} ₽"`

---

## 7. Сервисы

### 7.1. Метод в `IReportService`

```csharp
Task<ReconciliationData> GetReconciliationDataAsync(DateTime date, CancellationToken ct = default);
```

### 7.2. DTO

```csharp
public class ReconciliationData
{
    public DateTime Date { get; set; }
    public decimal CashAmount { get; set; }
    public decimal NonCashAmount { get; set; }
    public decimal CreditDebt { get; set; }
    public decimal NextPaymentAmount { get; set; }
    public DateTime? NextPaymentDate { get; set; }
}
```

### 7.3. Реализация в `ReportService`

- Получить наличные и безналичные через `TransactionService.GetPeriodTotalsAsync(date, date, PaymentType.*)`.
- Получить кредитный долг через `CreditCardService.GetTotalDebtAsync()`.
- Для ближайшего платежа: перебрать активные карты, взять минимальный `GetNextPaymentDateAsync`, суммировать `GetMinimumPaymentAsync` на эту дату.

---

## 8. Навигация

### 8.1. Регистрация в DI

В `App.xaml.cs` → `ConfigureServices`:

```csharp
services.AddTransient<ReconciliationViewModel>();
```

### 8.2. DataTemplate

В `MainWindow.xaml` в ресурсах `ContentControl`:

```xml
<DataTemplate DataType="{x:Type vm:ReconciliationViewModel}">
    <views:ReconciliationView/>
</DataTemplate>
```

### 8.3. Кнопка в сайдбаре

В `MainWindow.xaml` добавить иконку + подпись «Сверка». В `MainWindowViewModel` добавить команду навигации:

```csharp
NavigateReconciliationCommand = new RelayCommand(() => _navigationService.NavigateTo<ReconciliationViewModel>());
```

---

## 9. Пошаговая реализация

| № | Шаг | Файл |
|---|-----|------|
| 1 | Создать DTO `ReconciliationData` | `Models/Reports/ReconciliationData.cs` |
| 2 | Добавить метод `GetReconciliationDataAsync` в `IReportService` | `Services/IReportService.cs` |
| 3 | Реализовать метод в `ReportService` | `Services/ReportService.cs` |
| 4 | Создать `ReconciliationViewModel` | `ViewModels/ReconciliationViewModel.cs` |
| 5 | Создать `ReconciliationView.xaml` + code-behind | `Views/ReconciliationView.xaml` |
| 6 | Зарегистрировать VM в DI | `App.xaml.cs` |
| 7 | Добавить `DataTemplate` | `MainWindow.xaml` |
| 8 | Добавить кнопку навигации | `MainWindow.xaml` + `MainWindowViewModel.cs` |
| 9 | Собрать и протестировать | — |

---

## 10. Примерный макет

```
┌─────────────────────────────────────────────────────────────┐
│  Сверка кассы                    [дата]  [Сегодня]         │
├─────────────────────────────────────────────────────────────┤
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐   │
│  │   Наличные    │  │  Безналичные  │  │Кредитный долг │   │
│  │               │  │               │  │               │   │
│  │   45 000 ₽    │  │  120 000 ₽    │  │   24 033 ₽    │   │
│  │               │  │               │  │               │   │
│  │ пересчитайте  │  │ сверьте с     │  │ платёж        │   │
│  │ в кассе       │  │ выпиской      │  │ 1 202 ₽       │   │
│  └───────────────┘  └───────────────┘  └───────────────┘   │
│                                                             │
│  ┌─────────────────────┐  ┌─────────────────────────────┐  │
│  │  Фактический остаток│  │      Чистый баланс          │  │
│  │                     │  │                             │  │
│  │     165 000 ₽       │  │        140 967 ₽            │  │
│  │                     │  │                             │  │
│  │ Наличные +          │  │ 165 000 ₽ − 24 033 ₽        │  │
│  │ Безналичные         │  │                             │  │
│  └─────────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 11. Особенности

- Окно **только для просмотра**, никакого ввода и сохранения.
- Все суммы форматируются через `DecimalToStringConverter`.
- Цвет «Чистого баланса» зависит от знака через `IsNegativeConverter`.
- Карточки используют стандартную тень и скругление приложения.
- Дата сверки по умолчанию — сегодня.
