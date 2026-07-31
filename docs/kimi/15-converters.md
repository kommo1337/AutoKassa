# 15. XAML-конвертеры — каталог

> Полный каталог конвертеров из [`AutoKassa/Helpers/Converters`](../../AutoKassa/Helpers/Converters): назначение, входные/выходные типы, особенности (культура, кеширование, nullable-семантика) и реальные примеры использования. Краткая сводка также есть в [`08-views.md`](08-views.md) §4; соглашения по коду — в [`13-conventions.md`](13-conventions.md); типичные ошибки — в [`TRAPS.md`](TRAPS.md).

---

## 1. Регистрация конвертеров

Все конвертеры лежат в пространстве имён `AutoKassa.Helpers.Converters` и подключаются в XAML через xmlns:

```xml
xmlns:converters="clr-namespace:AutoKassa.Helpers.Converters"
```

> В [`SettingsView.xaml`](../../AutoKassa/Views/SettingsView.xaml) используется сокращённый префикс `conv:` — это тот же namespace.

### 1.1. Глобальная регистрация в `App.xaml`

15 из 18 конвертеров зарегистрированы как ресурсы приложения в [`App.xaml`](../../AutoKassa/App.xaml) (строки 47–61) и доступны в любом View без локального объявления:

```xml
<converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
<converters:InverseBoolConverter x:Key="InverseBoolConverter"/>
<converters:DecimalToStringConverter x:Key="DecimalToStringConverter"/>
<converters:OperationTypeToColorConverter x:Key="OperationTypeToColorConverter"/>
<converters:OperationTypeToTextConverter x:Key="OperationTypeToTextConverter"/>
<converters:IsNegativeConverter x:Key="IsNegativeConverter"/>
<converters:StringToVisibilityConverter x:Key="StringToVisibilityConverter"/>
<converters:PaymentTypeToIconConverter x:Key="PaymentTypeToIconConverter"/>
<converters:PaymentTypeToTextConverter x:Key="PaymentTypeToTextConverter"/>
<converters:CategoryColorConverter x:Key="CategoryColorConverter"/>
<converters:TransactionAmountConverter x:Key="TransactionAmountConverter"/>
<converters:TransactionDotColorConverter x:Key="TransactionDotColorConverter"/>
<converters:DebtStatusToTextConverter x:Key="DebtStatusToTextConverter"/>
<converters:DebtStatusToColorConverter x:Key="DebtStatusToColorConverter"/>
<converters:CounterpartyTypeToTextConverter x:Key="CounterpartyTypeToTextConverter"/>
```

### 1.2. Конвертеры без глобальной регистрации

Три конвертера в `App.xaml` **не** зарегистрированы — они объявляются локально в ресурсах конкретного View:

| Конвертер | Где зарегистрирован |
|---|---|
| `EqualityConverter` | [`TransactionEditView.xaml`](../../AutoKassa/Views/TransactionEditView.xaml) (строка 11) |
| `IntEqualityConverter` | [`SettingsView.xaml`](../../AutoKassa/Views/SettingsView.xaml) (строка 9, префикс `conv:`) |
| `IntToStringConverter` | [`SettingsView.xaml`](../../AutoKassa/Views/SettingsView.xaml) (строка 10, префикс `conv:`) |

### 1.3. Локальное дублирование глобальных ресурсов

Многие View повторно объявляют конвертеры, уже зарегистрированные в `App.xaml` (например, `BoolToVisibilityConverter` — в `CategoriesView`, `CounterpartiesView`, `CreditCardsView` и др.; полный набор — в `DebtReportView`, строки 8–15). Это избыточно: локальный ресурс с тем же ключом просто перекрывает глобальный идентичным экземпляром. При создании новых View на это не ориентируйся — используй глобальные ресурсы напрямую (см. §4).

---

## 2. Каталог конвертеров

### 2.1. Сводная таблица

| Конвертер | Интерфейс | Вход → Выход | Назначение | Глобально |
|---|---|---|---|---|
| `BoolToVisibilityConverter` | `IValueConverter` | `bool` / любой объект → `Visibility` | Видимость по булеву/ненулевому значению; параметр `Inverse` | Да |
| `InverseBoolConverter` | `IValueConverter` | `bool` → `bool` | Логическое отрицание (зарегистрирован, но не используется) | Да |
| `DecimalToStringConverter` | `IValueConverter` | `decimal` ↔ `string` | Денежная сумма «N2 ₽», культура `CurrentCulture` | Да |
| `IntToStringConverter` | `IValueConverter` | `object` ↔ `string` | `ToString()` / `int.TryParse` для `ComboBox.SelectedValue` | Нет |
| `IntEqualityConverter` | `IValueConverter` | `int` + параметр → `bool` | Привязка `RadioButton` к `int`-индексу вкладки | Нет |
| `EqualityConverter` | `IMultiValueConverter` | `object[]` → `bool` | Равенство двух значений (`Equals`) | Нет |
| `StringToVisibilityConverter` | `IValueConverter` | `string` → `Visibility` | Непустая строка → `Visible` | Да |
| `IsNegativeConverter` | `IValueConverter` | число → `bool` | `true`, если значение < 0 (для `DataTrigger`) | Да |
| `OperationTypeToColorConverter` | `IValueConverter` | `OperationType` → `SolidColorBrush` | Доход — зелёный, расход — красный | Да |
| `OperationTypeToTextConverter` | `IValueConverter` | `OperationType` → `string` | «Доход» / «Расход» | Да |
| `PaymentTypeToIconConverter` | `IValueConverter` | `PaymentType` → `string` | Эмодзи-иконка типа оплаты (не используется) | Да |
| `PaymentTypeToTextConverter` | `IValueConverter` | `PaymentType` → `string` | Русская метка типа оплаты | Да |
| `CategoryColorConverter` | `IValueConverter` | `string` (HEX) → `SolidColorBrush` | Цвет категории с кешем и `Freeze()` | Да |
| `TransactionAmountConverter` | `IMultiValueConverter` | `(decimal, OperationType)` → `string` | Сумма со знаком, жёстко `ru-RU`, `N0` | Да |
| `TransactionDotColorConverter` | `IMultiValueConverter` | `(OperationType, string)` → `SolidColorBrush` | Цветная точка операции с кешем и `Freeze()` | Да |
| `DebtStatusToTextConverter` | `IValueConverter` | `DebtStatus` → `string` | Русская метка статуса долга | Да |
| `DebtStatusToColorConverter` | `IValueConverter` | `DebtStatus` → `SolidColorBrush` | Цвет статуса долга (без кеша) | Да |
| `CounterpartyTypeToTextConverter` | `IValueConverter` | `CounterpartyType` → `string` | Русская метка типа контрагента | Да |

---

### 2.2. `BoolToVisibilityConverter`

Файл: [`BoolToVisibilityConverter.cs`](../../AutoKassa/Helpers/Converters/BoolToVisibilityConverter.cs)

Самый используемый конвертер проекта. Приведение значения к `bool`:

- `bool` — как есть;
- непустая строка → `true` (поэтому подходит для показа ошибок валидации: `Visibility="{Binding NameError, ...}"`);
- любой другой **не-null** объект → `true` (в т.ч. `int`, `DateTime?` со значением, выбранный элемент списка);
- `null` → `false`.

`true` → `Visibility.Visible`, `false` → `Visibility.Collapsed` (`Hidden` не используется). Параметр `"inverse"` (регистр не важен) инвертирует результат. `ConvertBack`: `Visible` → `true`, иначе `false`.

```xml
<!-- TransactionsView.xaml:1310 — пустое состояние списка -->
<TextBlock Visibility="{Binding HasTransactions, Converter={StaticResource BoolToVisibilityConverter}, ConverterParameter=inverse}" .../>

<!-- CreditCardEditView.xaml:58 — строка ошибки видна, пока текст не пуст -->
<TextBlock Visibility="{Binding NameError, Converter={StaticResource BoolToVisibilityConverter}}" .../>
```

---

### 2.3. `InverseBoolConverter`

Файл: [`InverseBoolConverter.cs`](../../AutoKassa/Helpers/Converters/InverseBoolConverter.cs)

`bool` → `!bool`. Нестандартный (не `bool`) вход: `Convert` → `true`, `ConvertBack` → `false`. Зарегистрирован в `App.xaml` и локально в [`LockScreenView.xaml`](../../AutoKassa/Views/LockScreenView.xaml) (строка 17), но **ни одна привязка его сейчас не использует** — фактическая инверсия везде делается через `BoolToVisibilityConverter` с `ConverterParameter=Inverse`.

---

### 2.4. `DecimalToStringConverter`

Файл: [`DecimalToStringConverter.cs`](../../AutoKassa/Helpers/Converters/DecimalToStringConverter.cs)

`decimal` → строка формата `N2` с символом рубля: `decimalValue.ToString("N2", CultureInfo.CurrentCulture) + " ₽`. Не-`decimal` вход → `"0,00 ₽"`.

**Культура — `CurrentCulture`**: на машине с `ru-RU` получится `1 234,56 ₽`, с `en-US` — `1,234.56 ₽`. Это осознанное отличие от `TransactionAmountConverter` (§2.14), где культура зашита.

`ConvertBack` убирает `₽` и пробелы и парсит через `decimal.TryParse(..., NumberStyles.Any, CurrentCulture)`; при неудаче возвращает `0m`.

```xml
<!-- DashboardView.xaml:795 -->
<TextBlock Text="{Binding Amount, Converter={StaticResource DecimalToStringConverter}}" .../>

<!-- CounterpartiesView.xaml:284 -->
<Binding Path="ActiveDebtAmount" Converter="{StaticResource DecimalToStringConverter}"/>
```

---

### 2.5. `IntToStringConverter`

Файл: [`IntToStringConverter.cs`](../../AutoKassa/Helpers/Converters/IntToStringConverter.cs)

`Convert`: `value?.ToString() ?? string.Empty` (nullable-вход безопасен). `ConvertBack`: `int.TryParse`, при неудаче → `0`. Используется для синхронизации `ComboBox.SelectedValue` (строка) с `int`-свойством настроек. Только локальная регистрация (см. §1.2).

```xml
<!-- SettingsView.xaml:382 -->
<ComboBox SelectedValue="{Binding DefaultOperationType, Converter={StaticResource IntToStringConverter}}" .../>
```

---

### 2.6. `IntEqualityConverter`

Файл: [`IntEqualityConverter.cs`](../../AutoKassa/Helpers/Converters/IntEqualityConverter.cs)

`Convert`: `true`, если `value is int` и равно числу из строкового `ConverterParameter`; иначе `false`. `ConvertBack`: при `true` возвращает число из параметра, иначе `Binding.DoNothing` — это паттерн «группа RadioButton ↔ одно int-свойство». Только локальная регистрация (см. §1.2).

```xml
<!-- SettingsView.xaml:219 — вкладки настроек -->
<RadioButton IsChecked="{Binding SelectedTabIndex, Converter={StaticResource IntEqualityConverter}, ConverterParameter=0}" .../>
```

---

### 2.7. `EqualityConverter`

Файл: [`EqualityConverter.cs`](../../AutoKassa/Helpers/Converters/EqualityConverter.cs)

Единственный `IMultiValueConverter` общего назначения: `Equals(values[0], values[1])`; меньше двух значений → `false`. `ConvertBack` бросает `NotSupportedException`. Только локальная регистрация (см. §1.2). Используется для подсветки выбранной категории в списке.

```xml
<!-- TransactionEditView.xaml:718 — подсветка выбранной категории -->
<MultiBinding Converter="{StaticResource EqualityConverter}">
    <Binding/>
    <Binding Path="DataContext.SelectedCategory" RelativeSource="{RelativeSource AncestorType=UserControl}"/>
</MultiBinding>
```

---

### 2.8. `StringToVisibilityConverter`

Файл: [`StringToVisibilityConverter.cs`](../../AutoKassa/Helpers/Converters/StringToVisibilityConverter.cs)

Пустая (`null`/`""`) строка → `Collapsed`, непустая → `Visible`. Любой не-строковый вход приводится через `as string` → `null` → `Collapsed`.

⚠️ **`ConverterParameter` игнорируется** — параметр `Inverse` не поддерживается (см. §4). Для инверсии используй `BoolToVisibilityConverter`.

```xml
<!-- DashboardView.xaml:770 — описание операции скрыто, если пустое -->
<TextBlock Visibility="{Binding Description, Converter={StaticResource StringToVisibilityConverter}}" .../>

<!-- ChangePasswordView.xaml:39 — блок ошибки -->
<TextBlock Visibility="{Binding CurrentPasswordError, Converter={StaticResource StringToVisibilityConverter}}" .../>
```

---

### 2.9. `IsNegativeConverter`

Файл: [`IsNegativeConverter.cs`](../../AutoKassa/Helpers/Converters/IsNegativeConverter.cs)

`true`, если число < 0. Поддерживаемые типы: `decimal`, `double`, `int`, `float`; всё остальное → `false`. Основной сценарий — `DataTrigger` для окраски отрицательного баланса в красный.

```xml
<!-- DashboardView.xaml:181 -->
<DataTrigger Binding="{Binding TodayBalance, Converter={StaticResource IsNegativeConverter}}" Value="True">
    <Setter Property="Foreground" Value="{StaticResource AccentRedBrush}"/>
</DataTrigger>
```

---

### 2.10. `OperationTypeToColorConverter`

Файл: [`OperationTypeToColorConverter.cs`](../../AutoKassa/Helpers/Converters/OperationTypeToColorConverter.cs)

`OperationType.Income` → `#22c55e` (зелёный), любое другое значение enum → `#ef4444` (красный), не-enum вход → `Gray`. Кисти — статические общие экземпляры (без `Freeze`, в отличие от `CategoryColorConverter`).

```xml
<!-- TransactionsView.xaml:1871 — цвет суммы операции -->
<TextBlock Foreground="{Binding Transaction.Type, Converter={StaticResource OperationTypeToColorConverter}}" .../>
```

---

### 2.11. `OperationTypeToTextConverter`

Файл: [`OperationTypeToTextConverter.cs`](../../AutoKassa/Helpers/Converters/OperationTypeToTextConverter.cs)

`Income` → `"Доход"`, любое другое значение enum → `"Расход"`, не-enum вход → `string.Empty`.

```xml
<!-- CategoriesView.xaml:255 -->
<TextBlock Text="{Binding Type, Converter={StaticResource OperationTypeToTextConverter}}" .../>
```

---

### 2.12. `PaymentTypeToIconConverter` и `PaymentTypeToTextConverter`

Файлы: [`PaymentTypeToIconConverter.cs`](../../AutoKassa/Helpers/Converters/PaymentTypeToIconConverter.cs), [`PaymentTypeToTextConverter.cs`](../../AutoKassa/Helpers/Converters/PaymentTypeToTextConverter.cs)

| `PaymentType` | Иконка | Текст |
|---|---|---|
| `Cash` | 💵 | Наличные |
| `NonCash` | 💳 | Безналичные |
| `CreditCard` | 💳 | Кредит |
| `Debt` | 🤝 | В долг |
| любое другое / не-enum | 💵 | Наличные |

`PaymentTypeToIconConverter` зарегистрирован в `App.xaml` и локально в `DashboardView.xaml` (строка 25), но **не используется ни одной привязкой** — иконки оплаты в списках переключаются через `DataTrigger` по `PaymentType` и шаблоны `CashPaymentIconTemplate` / `NonCashPaymentIconTemplate` / `CreditCardPaymentIconTemplate` (см. `DashboardView.xaml:778–790`, `TransactionsView.xaml:1852–1864`).

```xml
<!-- DashboardView.xaml:777 — текстовая метка в ToolTip иконки оплаты -->
<ContentControl ToolTip="{Binding PaymentType, Converter={StaticResource PaymentTypeToTextConverter}}" .../>
```

---

### 2.13. `CategoryColorConverter`

Файл: [`CategoryColorConverter.cs`](../../AutoKassa/Helpers/Converters/CategoryColorConverter.cs)

HEX-строка (`#22c55e`) → `SolidColorBrush`. Особенности:

- Кисти кешируются в статическом `ConcurrentDictionary<string, SolidColorBrush>` — каждый цвет создаётся один раз;
- кешированные кисти **замораживаются (`Freeze()`)** — безопасны для чтения из любого потока и не давят на GC при обновлении списков;
- `null`, пустая строка или невалидный HEX (исключение `ColorConverter.ConvertFromString`) → дефолтная кисть `#94a3b8` (серо-голубая; статическая, без `Freeze`);
- `ConvertBack` бросает `NotImplementedException`.

```xml
<!-- CategoriesView.xaml:248 — точка цвета категории -->
<Ellipse Fill="{Binding Color, Converter={StaticResource CategoryColorConverter}}"/>
```

---

### 2.14. `TransactionAmountConverter`

Файл: [`TransactionAmountConverter.cs`](../../AutoKassa/Helpers/Converters/TransactionAmountConverter.cs)

`IMultiValueConverter`: `(decimal Amount, OperationType Type)` → строка `"+12 222 ₽"` / `"−7 800 ₽"`. Особенности:

- культура **жёстко зашита**: `new CultureInfo("ru-RU")`, формат `N0` (без копеек, разделитель тысяч — неразрывный пробел) — не зависит от `CurrentCulture` машины;
- знак расхода — типографский минус `−` (U+2212), не дефис;
- некорректный вход (меньше двух значений, не-`decimal`, не-enum) → `"0 ₽"`;
- `ConvertBack` бросает `NotImplementedException`.

```xml
<!-- TransactionsView.xaml:1873 -->
<MultiBinding Converter="{StaticResource TransactionAmountConverter}">
    <Binding Path="Transaction.Amount"/>
    <Binding Path="Transaction.Type"/>
</MultiBinding>
```

Аналогично в [`TransactionDetailReportView.xaml`](../../AutoKassa/Views/Reports/TransactionDetailReportView.xaml) (строка 536). Цвет суммы при этом задаётся отдельно через `OperationTypeToColorConverter` (§2.10).

---

### 2.15. `TransactionDotColorConverter`

Файл: [`TransactionDotColorConverter.cs`](../../AutoKassa/Helpers/Converters/TransactionDotColorConverter.cs)

`IMultiValueConverter`: `(OperationType Type, string CategoryColor)` → `SolidColorBrush` для точки-маркера операции:

- `Expense` → красная кисть `#ef4444` (статическая, `Freeze`);
- `Income` + валидный HEX → кисть цвета категории из кеша `ConcurrentDictionary` (создаётся один раз, `Freeze`);
- `Income` без цвета / невалидный HEX / любой некорректный вход → зелёная кисть `#22c55e` (статическая, `Freeze`).

```xml
<!-- TransactionsView.xaml:1819 -->
<MultiBinding Converter="{StaticResource TransactionDotColorConverter}">
    <Binding Path="Transaction.Type"/>
    <Binding Path="Transaction.Category.Color"/>
</MultiBinding>
```

---

### 2.16. `DebtStatusToTextConverter` и `DebtStatusToColorConverter`

Файлы: [`DebtStatusToTextConverter.cs`](../../AutoKassa/Helpers/Converters/DebtStatusToTextConverter.cs), [`DebtStatusToColorConverter.cs`](../../AutoKassa/Helpers/Converters/DebtStatusToColorConverter.cs)

| `DebtStatus` | Текст | Цвет |
|---|---|---|
| `NotDebt` | — | чёрный (ветка default) |
| `Active` | Активен | `Orange` |
| `Repaid` | Погашён | `Green` |
| `WrittenOff` | Списан | `Gray` |
| не-enum вход | Неизвестно | чёрный |

⚠️ `DebtStatusToColorConverter` **создаёт новую кисть при каждом вызове** — кеширования и `Freeze` нет (в отличие от `CategoryColorConverter`). Не используй его в горячих списках без необходимости.

```xml
<!-- DebtReportView.xaml:503–505 -->
<TextBlock Text="{Binding Status, Converter={StaticResource DebtStatusToTextConverter}}"
           Foreground="{Binding Status, Converter={StaticResource DebtStatusToColorConverter}}"/>
```

---

### 2.17. `CounterpartyTypeToTextConverter`

Файл: [`CounterpartyTypeToTextConverter.cs`](../../AutoKassa/Helpers/Converters/CounterpartyTypeToTextConverter.cs)

`Client` → «Клиент», `Branch` → «Филиал», `Supplier` → «Поставщик», `Other` → «Прочее»; неизвестное значение или не-enum вход → «Неизвестно».

```xml
<!-- CounterpartiesView.xaml:262 -->
<TextBlock Text="{Binding Type, Converter={StaticResource CounterpartyTypeToTextConverter}}" .../>
```

---

## 3. Правила добавления нового конвертера

1. Создай класс в [`AutoKassa/Helpers/Converters`](../../AutoKassa/Helpers/Converters), namespace `AutoKassa.Helpers.Converters`; реализуй `IValueConverter` (одно значение) или `IMultiValueConverter` (несколько привязок).
2. Добавь русский XML-комментарий `/// <summary>` — обязателен для public API по [`13-conventions.md`](13-conventions.md) §2.
3. Зарегистрируй в [`App.xaml`](../../AutoKassa/App.xaml) рядом с остальными (ключ = имя класса). Локальная регистрация в конкретном View допустима только для конвертера, нужного одному экрану (прецеденты: `EqualityConverter`, `IntEqualityConverter`, `IntToStringConverter`).
4. Возвращаешь `Brush`? Следуй паттерну `CategoryColorConverter`: статический `ConcurrentDictionary`-кеш + `Freeze()`. Не создавай кисть на каждый вызов в конвертерах для списков.
5. Деньги: реши и задокументируй культуру — универсальный формат через `CurrentCulture` (паттерн `DecimalToStringConverter`) или стабильный `ru-RU` (паттерн `TransactionAmountConverter`). Не смешивай оба подхода в одном конвертере.
6. `ConvertBack`: если обратное преобразование не нужно — бросай `NotImplementedException` (сложившаяся практика проекта). Реализуй `ConvertBack` только если конвертер будет использоваться в `TwoWay`-привязке.
7. Обнови этот файл и краткую таблицу в [`08-views.md`](08-views.md) §4.
8. Прогони `dotnet build`.

---

## 4. Распространённые ошибки

- **Передать `ConverterParameter=Inverse` в `StringToVisibilityConverter`.** Параметр игнорируется, а не-строковое значение (`int`, коллекция) даст `value as string == null` → **всегда `Collapsed`**. Так были устроены плашки «Нет расходов/доходов за период» в [`DashboardView.xaml`](../../AutoKassa/Views/DashboardView.xaml): они привязывали `TopExpenses.Count` (`int`) и никогда не показывались — исправлено переводом на булевы свойства `HasTopExpenses`/`HasTopIncomes` во ViewModel + `BoolToVisibilityConverter` с `ConverterParameter=Inverse`. Для инверсии и не-строковых значений используй именно этот паттерн. См. также [`TRAPS.md`](TRAPS.md).
- **Считать, что `BoolToVisibilityConverter` принимает только `bool`.** Любой не-null объект считается `true`: упакованный `int 0`, `DateTime`, выбранный элемент списка — всё это даст `Visible`. Строгое `false` дают только `null`, `false` и пустая строка. Если нужна строгая булева семантика — приводи значение во ViewModel.
- **Смешать культуры форматирования.** `DecimalToStringConverter` зависит от `CurrentCulture` (`1 234,56 ₽` vs `1,234.56 ₽`), а `TransactionAmountConverter` всегда `ru-RU`/`N0`. Рядом в одном списке форматы могут различаться — это ожидаемо, не «баг конвертера».
- **Парсить строку суммы вручную.** Знак расхода в `TransactionAmountConverter` — U+2212 (`−`), а не дефис `-`; разделитель тысяч в `ru-RU` `N0` — неразрывный пробел.
- **Забыть регистрацию.** `EqualityConverter`, `IntEqualityConverter`, `IntToStringConverter` отсутствуют в `App.xaml` — без локального объявления в ресурсах View получишь `XamlParseException`/`StaticResource` не найден.
- **Мутировать закешированную кисть.** Кисти из `CategoryColorConverter`/`TransactionDotColorConverter` заморожены — попытка изменить `Color` бросит `InvalidOperationException`. Нужна изменяемая кисть — создай свою, не трогай кеш.
- **Ожидать кеш там, где его нет.** `DebtStatusToColorConverter` и `OperationTypeToColorConverter` кеш не используют так, как `CategoryColorConverter`: первый создаёт кисть на каждый вызов, второй отдаёт статические незамороженные экземпляры.
- **Дублировать регистрацию.** Повторное объявление глобального конвертера в `UserControl.Resources` избыточно (см. §1.3) — новые View должны ссылаться на ресурсы `App.xaml` напрямую.
- **Использовать конвертер в `TwoWay`-привязке без `ConvertBack`.** Большинство конвертеров бросают `NotImplementedException`/`NotSupportedException`. Рабочий `ConvertBack` есть только у `BoolToVisibilityConverter`, `InverseBoolConverter`, `DecimalToStringConverter`, `IntToStringConverter`, `IntEqualityConverter`.
- **Ориентироваться на `InverseBoolConverter` и `PaymentTypeToIconConverter` как на образец.** Оба зарегистрированы, но фактически не используются (см. §2.3, §2.12).

---

## 5. Связь с другими файлами

- [`08-views.md`](08-views.md) — общие правила Views, глобальные ресурсы `App.xaml`, краткая таблица конвертеров (§4).
- [`13-conventions.md`](13-conventions.md) — соглашения по коду: XML-документация, именование, порядок добавления новых элементов.
- [`TRAPS.md`](TRAPS.md) — ловушки конвертеров (`BoolToVisibilityConverter` и не-null, культура денег, кеш кистей).
- [`04-models.md`](04-models.md) — перечисления `OperationType`, `PaymentType`, `DebtStatus`, `CounterpartyType`, на которые опираются enum-конвертеры.
