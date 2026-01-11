# Задача: Создать окно настроек приложения

## Контекст
Проект AutoKassa - WPF приложение для учета личных финансов на .NET 8.0 с MVVM архитектурой.
В текущей версии настройки частично разбросаны по коду. Необходимо создать централизованный 
интерфейс управления настройками.

## Требования

### 1. Обновление главного окна (MainWindow.xaml)
**Изменения в боковом меню:**
- УДАЛИТЬ кнопку "💰 Операции" из бокового меню
- ДОБАВИТЬ кнопку "⚙️ Настройки" в боковое меню
- Порядок кнопок: Операции (скрыта), Категории, Отчеты, Настройки

### 2. Модель Settings (Models/Settings.cs)
Расширить существующую модель Settings, добавив:
```csharp
// Общие настройки
public bool AutoLock { get; set; } = true;
public int AutoLockMinutes { get; set; } = 5;
public bool ShowNotifications { get; set; } = true;
public string Theme { get; set; } = "Light"; // Light, Dark

// Настройки операций
public bool ShowOperationsInSidebar { get; set; } = false; // Новое свойство
public int DefaultPageSize { get; set; } = 20;
public bool ConfirmDelete { get; set; } = true;

// Настройки отчетов
public string DefaultReportPeriod { get; set; } = "Month"; // Today, Week, Month, Quarter, Year
public bool AutoGenerateReports { get; set; } = false;

// Настройки резервного копирования
public bool AutoBackup { get; set; } = false;
public int AutoBackupDays { get; set; } = 7;
public string BackupPath { get; set; } = "";

// Настройки безопасности
public bool RequirePasswordOnStartup { get; set; } = true;
public int PasswordExpireDays { get; set; } = 0; // 0 = никогда

// Настройки интерфейса
public string Language { get; set; } = "ru-RU";
public double WindowWidth { get; set; } = 1200;
public double WindowHeight { get; set; } = 700;
```

### 3. Обновление ISettingsService (Services/ISettingsService.cs)
Добавить методы:
```csharp
Task<Settings> GetSettingsAsync();
Task SaveSettingsAsync(Settings settings);
Task ResetToDefaultsAsync();
Task<bool> ExportSettingsAsync(string filePath);
Task<bool> ImportSettingsAsync(string filePath);
```

### 4. Обновление SettingsService (Services/SettingsService.cs)
Реализовать новые методы из интерфейса.

### 5. SettingsViewModel (ViewModels/SettingsViewModel.cs)
Создать ViewModel со следующей структурой:

**Свойства:**
- Settings CurrentSettings
- bool IsLoading
- bool HasUnsavedChanges
- ObservableCollection<string> AvailableThemes
- ObservableCollection<string> AvailableLanguages

**Команды:**
- SaveCommand - сохранить настройки
- ResetCommand - сбросить к значениям по умолчанию
- CancelCommand - отменить изменения
- ExportCommand - экспорт настроек в JSON
- ImportCommand - импорт настроек из JSON
- ChangePasswordCommand - изменить пароль
- SelectBackupPathCommand - выбор папки для резервных копий
- CreateBackupCommand - создать резервную копию сейчас
- RestoreBackupCommand - восстановить из резервной копии

**Разделы настроек (для табов):**
- Общие
- Операции
- Отчеты
- Резервное копирование
- Безопасность
- Интерфейс

### 6. SettingsView (Views/SettingsView.xaml)
Создать представление со следующей структурой:

**Layout:**
```
┌─────────────────────────────────────────────────────────┐
│ ⚙️ Настройки                          [Сохранить] [Отмена] │
├──────────────┬──────────────────────────────────────────┤
│ Общие        │                                          │
│ Операции     │  СОДЕРЖИМОЕ ВЫБРАННОЙ ВКЛАДКИ           │
│ Отчеты       │                                          │
│ Резерв. копир│                                          │
│ Безопасность │                                          │
│ Интерфейс    │                                          │
│              │                                          │
│──────────────┤                                          │
│ [Экспорт]    │                                          │
│ [Импорт]     │                                          │
│ [Сброс]      │                                          │
└──────────────┴──────────────────────────────────────────┘
```

**Вкладка "Общие":**
- CheckBox: Автоблокировка экрана
- NumericUpDown: Время неактивности (мин)
- CheckBox: Показывать уведомления
- ComboBox: Тема оформления (Light/Dark)

**Вкладка "Операции":**
- CheckBox: "Показывать 'Операции' в боковом меню" (привязка к ShowOperationsInSidebar)
- NumericUpDown: Количество записей на странице
- CheckBox: Подтверждать удаление

**Вкладка "Отчеты":**
- ComboBox: Период по умолчанию
- CheckBox: Автоматически формировать отчеты

**Вкладка "Резервное копирование":**
- CheckBox: Автоматическое резервное копирование
- NumericUpDown: Интервал (дней)
- TextBox + Button: Путь для сохранения
- Button: Создать резервную копию сейчас
- Button: Восстановить из резервной копии

**Вкладка "Безопасность":**
- CheckBox: Требовать пароль при запуске
- Button: Изменить пароль
- NumericUpDown: Срок действия пароля (дней, 0=никогда)

**Вкладка "Интерфейс":**
- ComboBox: Язык интерфейса (ru-RU, en-US)
- NumericUpDown: Ширина окна по умолчанию
- NumericUpDown: Высота окна по умолчанию

### 7. Обновление MainWindowViewModel (ViewModels/MainWindowViewModel.cs)

**Добавить:**
- Команду NavigateToSettingsCommand
- Метод NavigateToSettings()
- При загрузке настроек проверять ShowOperationsInSidebar и скрывать/показывать кнопку "Операции"

**Пример:**
```csharp
private async void LoadSettings()
{
    var settings = await _settingsService.GetSettingsAsync();
    ShowOperationsInSidebar = settings.ShowOperationsInSidebar;
}

public bool ShowOperationsInSidebar { get; set; }
```

### 8. Обновление MainWindow.xaml

**В боковом меню:**
```xml
<!-- Операции - показывать только если включено в настройках -->
<Button Command="{Binding NavigateToTransactionsCommand}"
        Visibility="{Binding ShowOperationsInSidebar, Converter={StaticResource BoolToVisibilityConverter}}">
    💰 Операции
</Button>

<!-- Категории -->
<Button Command="{Binding NavigateToCategoriesCommand}">
    📁 Категории
</Button>

<!-- Отчеты -->
<Button Command="{Binding NavigateToReportsCommand}">
    📊 Отчеты
</Button>

<!-- Настройки -->
<Button Command="{Binding NavigateToSettingsCommand}">
    ⚙️ Настройки
</Button>
```

### 9. Регистрация в App.xaml.cs
```csharp
services.AddTransient<SettingsViewModel>();
```

### 10. DataTemplate в MainWindow.xaml
```xml
<DataTemplate DataType="{x:Type vm:SettingsViewModel}">
    <views:SettingsView/>
</DataTemplate>
```

## Дизайн и стилистика

### Цвета (использовать из Colors.xaml):
- Заголовки: PrimaryBrush (#2196F3)
- Текст: ForegroundBrush
- Фон: BackgroundBrush
- Карточки: SurfaceBrush
- Успешные действия: SuccessBrush
- Предупреждения: WarningBrush

### Стили:
- Кнопки: использовать существующие стили из Buttons.xaml
- Отступы: 10-20px между элементами
- Border CornerRadius: 8
- Шрифты: Segoe UI

### TabControl стиль:
- Вертикальные вкладки слева
- Ширина панели табов: 200px
- Активная вкладка: светло-синий фон
- Hover эффект на вкладках

## Функциональность

### Валидация:
- AutoLockMinutes: 1-60
- DefaultPageSize: 10-100
- AutoBackupDays: 1-365
- PasswordExpireDays: 0-365

### Поведение:
- При изменении любого параметра: HasUnsavedChanges = true
- При SaveCommand: сохранить в БД, показать уведомление "Настройки сохранены"
- При CancelCommand: восстановить исходные значения
- При ResetCommand: показать диалог подтверждения
- При ChangePasswordCommand: открыть диалоговое окно смены пароля

### Экспорт/Импорт:
- Формат: JSON
- Экспорт: SaveFileDialog с фильтром *.json
- Импорт: OpenFileDialog с фильтром *.json, валидация структуры файла

## Миграция БД

Создать миграцию для добавления новых полей в таблицу Settings:
```bash
dotnet ef migrations add AddExpandedSettings
dotnet ef database update
```

## Тестирование

После реализации проверить:
1. ✅ Открытие окна настроек из главного меню
2. ✅ Переключение между вкладками
3. ✅ Сохранение настроек в БД
4. ✅ Отмена изменений
5. ✅ Сброс к значениям по умолчанию
6. ✅ Экспорт настроек в JSON
7. ✅ Импорт настроек из JSON
8. ✅ Изменение пароля
9. ✅ Выбор папки для резервных копий
10. ✅ Создание резервной копии
11. ✅ Скрытие/показ кнопки "Операции" в боковом меню
12. ✅ Применение настроек автоблокировки

## Примечания

- Следовать существующему стилю кодирования проекта
- Использовать async/await для работы с БД
- Добавить XML комментарии к public API
- Обработка ошибок через try-catch с показом DialogService
- Использовать существующие конвертеры из Helpers/Converters

## Приоритет задач

1. **Высокий** - создание базовой структуры (модель, сервис, ViewModel, View)
2. **Высокий** - вкладки "Общие" и "Операции" (включая ShowOperationsInSidebar)
3. **Средний** - остальные вкладки
4. **Средний** - экспорт/импорт настроек
5. **Низкий** - дополнительные валидации и улучшения UI

## Ожидаемый результат

После выполнения задачи:
- Пользователь может открыть настройки через боковое меню
- Все параметры приложения доступны для настройки
- Настройки сохраняются в БД и применяются немедленно
- Кнопка "Операции" скрыта по умолчанию, но может быть включена через настройки
- Возможность экспорта/импорта конфигурации
- UI соответствует общему стилю приложения