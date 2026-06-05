# Комплексный аудит безопасности AutoKassa

**Дата аудита:** 05.06.2026  
**Объект:** WPF-приложение AutoKassa (.NET 10, SQLite, EF Core)  
**Методология:** SAST (Static Application Security Testing), ручной анализ кода, проверка зависимостей  

---

## Общая оценка

Приложение **AutoKassa** демонстрирует **удовлетворительный уровень безопасности** для desktop-приложения класса «локальная учётная система». Критических уязвимостей (RCE, SQL-инъекция, хардкод учётных данных, десериализация небезопасных данных) **не выявлено**. Архитектура приложения предполагает оффлайн-работу, что существенно снижает поверхность атаки.

**Однако** обнаружены проблемы в области:
- Хранения паролей в оперативной памяти (managed-строки)
- Валидации пользовательского ввода в UI
- Политики паролей и криптографических настроек
- Логирования потенциально чувствительных данных

---

## Статистика по критичности

| Уровень | Количество |
|---------|------------|
| **Critical** | 0 |
| **High** | 4 |
| **Medium** | 8 |
| **Low** | 4 |
| **Информация** | 1 |

---

## Найденные уязвимости

### [HIGH] Пароли хранятся в памяти как обычные managed-строки (`string`)

- **Файлы:**
  - `AutoKassa/ViewModels/InitialSetupViewModel.cs` (строки 17, 54, 71)
  - `AutoKassa/ViewModels/LockScreenViewModel.cs` (строки 14, 36)
  - `AutoKassa/ViewModels/ChangePasswordViewModel.cs` (строки 16–18, 38–73)
  - `AutoKassa/Views/InitialSetupView.xaml.cs` (строка ~29)
  - `AutoKassa/Views/LockScreenView.xaml.cs` (строка ~35)
  - `AutoKassa/Views/ChangePasswordView.xaml.cs` (строки ~31, ~39, ~47)

- **Описание:** Во всех ViewModel пароли передаются и хранятся в свойствах типа `string`. В .NET строки являются иммутабельными объектами в managed heap, не подлежат принудительному обнулению и могут оставаться в памяти длительное время (до сборки мусора, а иногда и дольше из-за перемещения в Large Object Heap или стринг-пула). Аналогично, `PasswordBox.Password` возвращает `string`, который сразу присваивается VM.

- **Вектор атаки:** Злоумышленник с доступом к дампу памяти процесса (через другой процесс с правами отладки, создание minidump через `MiniDumpWriteDump`, или чтение файла подкачки/pagefile) может извлечь plaintext-пароли пользователя. Для финансового приложения это критично.

- **Рекомендация:** Использовать `SecureString` (или `Span<char>`/`char[]` с последующим обнулением) для промежуточного хранения паролей. В code-behind View извлекать `SecureString` из `PasswordBox.SecurePassword`, а в VM использовать `SecureString` или хотя бы `char[]` с ручным `Array.Clear()`.

```csharp
// Пример использования SecureString
public SecureString SecurePassword { get; set; }

// В code-behind:
_viewModel.SecurePassword = ((PasswordBox)sender).SecurePassword;

// Для передачи в BCrypt — временное копирование в char[]
var ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
try { /* использовать ptr */ }
finally { Marshal.ZeroFreeGlobalAllocUnicode(ptr); }
```

- **Ссылки:** CWE-316, OWASP Mobile Top 10 M2 (Insecure Data Storage), Microsoft Docs: SecureString

---

### [HIGH] Слабая политика паролей

- **Файлы:**
  - `AutoKassa/ViewModels/ChangePasswordViewModel.cs` (строка 127)
  - `AutoKassa/ViewModels/InitialSetupViewModel.cs` (строки 137–170)

- **Описание:** 
  1. В `ChangePasswordViewModel` минимальная длина пароля — **4 символа** (`NewPassword.Length < 4`). Это категорически недостаточно для приложения, содержащего финансовые данные.
  2. В `InitialSetupViewModel` индикатор «надёжности» (`PasswordStrength`) считает пароль длиной 8+ с цифрой и спецсимволом «сильным» (уровень 3). Однако отсутствует проверка на последовательности (`12345678`, `qwerty`), повторы (`11111111`) и словарные слова.

- **Вектор атаки:** Брутфорс пароля методом полного перебора или по словарю. При наличии доступа к хешу (через копию БД) пароль длиной 4–6 символов взламывается за секунды/минуты на современном оборудовании.

- **Рекомендация:** 
  - Увеличить минимальную длину до **10–12 символов**.
  - Добавить проверку энтропии (наличие прописных, строчных, цифр, спецсимволов).
  - Интегрировать проверку по словарю типичных паролей (например, через небольшой встроенный deny-list).

```csharp
// Пример улучшенной валидации
if (newPassword.Length < 10)
    return "Минимум 10 символов";
if (!newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsLower))
    return "Требуются прописные и строчные буквы";
if (!newPassword.Any(char.IsDigit))
    return "Требуется минимум 1 цифра";
if (CommonPasswords.Contains(newPassword)) // встроенный deny-list top-1000
    return "Слишком распространённый пароль";
```

- **Ссылки:** CWE-521, NIST SP 800-63B

---

### [HIGH] Не задан явный work factor для BCrypt

- **Файл:** `AutoKassa/Services/PasswordService.cs` (строка 10)

- **Описание:** `BCrypt.Net.BCrypt.GenerateSalt()` вызывается без параметров. В разных версиях библиотеки это может соответствовать work factor 10 или 11. По современным стандартам (2026 г.) для защиты финансовых данных рекомендуется **work factor ≥ 12** (желательно 13–14).

- **Вектор атаки:** При компрометации файла БД злоумышленник может осуществить offline-атаку на хеши паролей. Низкий work factor снижает время подбора в 4–16 раз.

- **Рекомендация:**

```csharp
return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
```

- **Ссылки:** CWE-916, OWASP Password Storage Cheat Sheet

---

### [HIGH] Нормализация ответа на секретный вопрос через `ToLower()` вместо `ToLowerInvariant()`

- **Файл:** `AutoKassa/ViewModels/InitialSetupViewModel.cs` (строка 322)

- **Описание:** `var answerHash = _passwordService.HashPassword(Answer.Trim().ToLower());`. Использование `ToLower()` вместо `ToLowerInvariant()` делает поведение зависимым от текущей культуры потока. В некоторых культурах (турецкая, азербайджанская) `ToLower()` может давать неожиданные результаты для символов «I» → «ı», что приведёт к невозможности восстановления пароля. Кроме того, приведение к нижнему регистру снижает энтропию ответа.

- **Вектор атаки:** Снижение энтропии секретного ответа упрощает его подбор. Также возможен Denial of Service (невозможность восстановить пароль при смене культуры ОС).

- **Рекомендация:** Использовать `ToLowerInvariant()` или отказаться от нормализации регистра в пользу точного совпадения.

```csharp
var answerHash = _passwordService.HashPassword(Answer.Trim().ToLowerInvariant());
```

- **Ссылки:** CWE-20, Microsoft Docs: Best Practices for Using Strings in .NET

---

### [MEDIUM] Отключена валидация данных в поле суммы операции

- **Файл:** `AutoKassa/Views/TransactionEditView.xaml` (строка 226)

- **Описание:** `Text="{Binding AmountText, UpdateSourceTrigger=PropertyChanged, ValidatesOnNotifyDataErrors=False}"`. Явно отключена встроенная валидация. Единственная защита — `PreviewTextInput` в code-behind, который **не блокирует вставку** (Ctrl+V) и не блокирует программное изменение текста.

- **Вектор атаки:** Пользователь может вставить некорректное значение (буквы, спецсимволы, очень длинную строку), что приведёт к необработанному исключению в `decimal.Parse` или некорректному поведению ViewModel.

- **Рекомендация:** Включить `ValidatesOnNotifyDataErrors=True` и добавить `ValidationRules` в XAML:

```xml
<TextBox Text="{Binding AmountText, UpdateSourceTrigger=PropertyChanged, ValidatesOnNotifyDataErrors=True}">
    <TextBox.Text>
        <Binding Path="AmountText" UpdateSourceTrigger="PropertyChanged">
            <Binding.ValidationRules>
                <local:DecimalValidationRule />
            </Binding.ValidationRules>
        </Binding>
    </TextBox.Text>
</TextBox>
```

- **Ссылки:** CWE-20, CWE-754

---

### [MEDIUM] Некорректная валидация пользовательского ввода в настройках (XAML)

- **Файлы:** `AutoKassa/Views/SettingsView.xaml` (строки 299–301, 321–323, 356–358, 475–477, 483–485, 538–541)

- **Описание:** Поля `AutoLockMinutes`, `InitialBalance`, `DefaultPageSize`, `AutoBackupDays`, `BackupKeepCount`, `PasswordExpireDays` используют `TextBox` без `ValidationRules`, `ValidatesOnDataErrors` и без `MaxLength`. Валидация выполняется только в коде VM (`ValidateSettings`), но некорректный ввод (например, текст в числовое поле) может вызвать исключение при конвертации типов.

- **Вектор атаки:** Возможность ввода некорректных данных, вызывающих исключения форматирования. При определённых условиях — UI-краш или запись некорректных значений в БД.

- **Рекомендация:** Добавить `ValidationRules` для числовых полей, ограничить `MaxLength`, использовать `ValidatesOnDataErrors=True`.

---

### [MEDIUM] `Process.Start` с `UseShellExecute = true` для открытия экспортированных файлов

- **Файлы:**
  - `AutoKassa/ViewModels/Reports/BalanceReportViewModel.cs` (строки 292, 308)
  - `AutoKassa/ViewModels/Reports/CategoryReportViewModel.cs` (строки 318, 331)
  - `AutoKassa/ViewModels/Reports/TransactionDetailReportViewModel.cs` (строки 239, 247)

- **Описание:** При открытии PDF/Excel отчётов используется `Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true })`. Хотя путь формируется из контролируемых данных (`_exportFolder` + сгенерированное имя), `UseShellExecute = true` позволяет ОС выбрать обработчик по ассоциации файлов. Если злоумышленник каким-либо образом скомпрометирует путь (например, через junction/symlink-атаку в папке Exports), возможен запуск произвольного файла.

- **Вектор атаки:** Теоретически — подмена файла отчёта в папке `Exports` между генерацией и открытием, или symlink-атака на папку `Exports`.

- **Рекомендация:** Проверять существование файла и его расширение перед открытием. Использовать whitelist:

```csharp
if (!File.Exists(filePath)) return;
var ext = Path.GetExtension(filePath).ToLowerInvariant();
if (ext != ".pdf" && ext != ".xlsx") return; // Whitelist
Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
```

- **Ссылки:** CWE-78, CWE-73

---

### [MEDIUM] Перезапуск приложения через `Process.Start(Environment.ProcessPath!)`

- **Файл:** `AutoKassa/ViewModels/SettingsViewModel.cs` (строка 759)

- **Описание:** После восстановления БД из бэкапа приложение перезапускает себя через `System.Diagnostics.Process.Start(Environment.ProcessPath!)`. Используется null-forgiving operator (`!`). Хотя `Environment.ProcessPath` в .NET 6+ не бывает `null` для нормального процесса, отсутствие проверки и потенциальная возможность модификации переменных окружения/PEB теоретически представляет риск.

- **Вектор атаки:** Если злоумышленник имеет возможность модифицировать переменные окружения процесса (через другой процесс с правами инжектора), можно подменить путь к исполняемому файлу.

- **Рекомендация:** Добавить проверку на `null` и валидацию пути:

```csharp
var processPath = Environment.ProcessPath;
if (string.IsNullOrEmpty(processPath) || !File.Exists(processPath))
{
    _dialogService.ShowError("Не удалось определить путь к приложению");
    return;
}
Process.Start(processPath);
```

- **Ссылки:** CWE-78

---

### [MEDIUM] `SanitizeBackupPath` не применяется при сохранении настроек через UI

- **Файлы:**
  - `AutoKassa/Services/SettingsService.cs` (строки 441–455, 325)
  - `AutoKassa/ViewModels/SettingsViewModel.cs` (строка 522)

- **Описание:** Метод `SanitizeBackupPath` вызывается **только** в `ImportSettingsAsync` (импорт JSON), но **не** при обычном сохранении через `SaveSettingsAsync` / `ApplyPropertiesToSettings`. Хотя в UI выбор пути происходит через диалог, прямое сохранение настроек (если бы злоумышленник имел доступ к памяти/БД) не санитизирует путь.

- **Вектор атаки:** При компрометации процесса или модификации БД напрямую можно записать произвольный путь в `BackupPath`, который затем будет использоваться для `CreateBackupAsync` и `CleanupOldBackups` без валидации.

- **Рекомендация:** Добавить вызов `SanitizeBackupPath` в `ApplyPropertiesToSettings` или в `SaveSettingsAsync`:

```csharp
settings.BackupPath = SanitizeBackupPath(
    string.IsNullOrWhiteSpace(BackupPath) ? null : BackupPath
);
```

- **Ссылки:** CWE-22, CWE-20

---

### [MEDIUM] `CleanupOldBackups` удаляет файлы по пути из настроек без валидации

- **Файл:** `AutoKassa/Services/SettingsService.cs` (строки 541–555)

- **Описание:** Метод `CleanupOldBackups` получает `backupDir` из настроек и выполняет `File.Delete(file)` для файлов по маске `AutoKassa_Backup_*.db`. Если `backupDir` скомпрометирован (path traversal), возможно удаление произвольных `.db` файлов.

- **Вектор атаки:** Если `BackupPath` указывает на системную папку (например, через инъекцию в БД или недостаточную санитизацию), авто-бэкап может удалить чужие `.db` файлы.

- **Рекомендация:** Применять `SanitizeBackupPath` к `backupDir` перед вызовом `CleanupOldBackups`, либо встроить валидацию в сам метод:

```csharp
backupDir = SanitizeBackupPath(backupDir);
if (string.IsNullOrEmpty(backupDir)) return;
```

- **Ссылки:** CWE-22, CWE-73

---

### [MEDIUM] Отсутствие `MaxLength` на полях ввода текста

- **Файлы:**
  - `AutoKassa/Views/CategoryManagerView.xaml` (строка ~171)
  - `AutoKassa/Views/InitialSetupView.xaml` (строки ~126, ~135)

- **Описание:** Поля для названия категории, пользовательского секретного вопроса и ответа на него не имеют ограничения `MaxLength` в XAML. Хотя в БД есть ограничения (`nvarchar`), отсутствие UI-валидации позволяет ввести экстремально длинные строки, что может вызвать проблемы с памятью или UI.

- **Вектор атаки:** DoS через ввод очень длинной строки (100K+ символов), вызывающей задержки рендеринга WPF или исключения EF Core при сохранении.

- **Рекомендация:** Добавить `MaxLength` в XAML, соответствующий ограничениям БД:

```xml
<TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" MaxLength="100"/>
```

- **Ссылки:** CWE-20, CWE-400

---

### [LOW] Логирование полных исключений с потенциальным раскрытием информации

- **Файл:** `AutoKassa/App.xaml.cs` (строки 38–55)

- **Описание:** Глобальные обработчики необработанных исключений (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) логируют полные объекты исключений через Serilog. Если в исключении содержатся SQL-запросы (EF Core), пути к файлам или внутренние параметры, они попадут в текстовый лог `logs/autokassa-*.log`.

- **Вектор атаки:** Злоумышленник с доступом к файловой системе (другой пользователь ОС, malware) может прочитать лог-файлы и извлечь структуру БД, пути, внутренние параметры.

- **Рекомендация:** Для production-режима либо ограничить детализацию логов, либо обеспечить защиту файлов логов через ACL:

```csharp
// Пример ограничения логов
Log.Fatal(args.Exception?.Message ?? "Unknown error", "Dispatcher error");
// Вместо: Log.Fatal(args.Exception, "Dispatcher error");
```

- **Ссылки:** CWE-532, CWE-209

---

### [LOW] Отсутствие манифеста UAC (`app.manifest`)

- **Файл:** проект `AutoKassa.csproj`

- **Описание:** В проекте отсутствует файл `app.manifest`. Приложение запускается с уровнем `asInvoker` (по умолчанию). Отсутствие явной декларации `requestedExecutionLevel` означает отсутствие контроля над виртуализацией реестра/файловой системы в старых версиях Windows.

- **Вектор атаки:** Не является прямой уязвимостью, но затрудняет аудит привилегий. В многопользовательской среде приложение работает с правами текущего пользователя без явного декларирования.

- **Рекомендация:** Добавить `app.manifest` с явным указанием `requestedExecutionLevel`:

```xml
<requestedExecutionLevel level="asInvoker" uiAccess="false" />
```

- **Ссылки:** Microsoft Docs: UAC Manifest

---

### [LOW] Интерполяция строки в connection string (SQLite)

- **Файлы:**
  - `AutoKassa/Services/AppDbContext.cs` (строка 49)
  - `AutoKassa/Services/SettingsService.cs` (строка 376)

- **Описание:** `optionsBuilder.UseSqlite($"Data Source={dbPath}");` и `new SqliteConnection($"Data Source={backupFilePath}")`. Хотя оба пути формируются из контролируемых источников (`BaseDirectory` / сгенерированное имя) и не содержат пользовательского ввода напрямую, использование интерполяции в connection string является антипаттерном.

- **Вектор атаки:** В текущей реализации — минимален. Однако при будущих изменениях путь может стать источником инъекции.

- **Рекомендация:** Использовать `SqliteConnectionStringBuilder`:

```csharp
var builder = new SqliteConnectionStringBuilder { DataSource = dbPath };
optionsBuilder.UseSqlite(builder.ConnectionString);
```

- **Ссылки:** CWE-89, Microsoft Docs: Connection String Builders

---

### [LOW] Не установлены ограниченные права доступа (ACL) для создаваемых папок

- **Файлы:**
  - `AutoKassa/Services/ExportService.cs` (строка 36)
  - `AutoKassa/Services/SettingsService.cs` (строка 369)

- **Описание:** `Directory.CreateDirectory` создаёт папки с правами по умолчанию (наследуемыми от родительской). В многопользовательской среде Windows другие пользователи системы могут иметь доступ к экспортированным отчётам и резервным копиям.

- **Вектор атаки:** Локальный повышение привилегий через чтение чувствительных финансовых данных другими пользователями ОС.

- **Рекомендация:** Устанавливать явные ACL при создании папок:

```csharp
var dirInfo = Directory.CreateDirectory(_exportFolder);
var security = dirInfo.GetAccessControl();
security.AddAccessRule(new FileSystemAccessRule(
    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
    FileSystemRights.Read, AccessControlType.Deny));
dirInfo.SetAccessControl(security);
```

- **Ссылки:** CWE-276, CWE-732

---

### [INFO] Client-side evaluation при поиске по описанию (Unicode)

- **Файл:** `AutoKassa/Services/TransactionService.cs` (строки 44–52, 69–76)

- **Описание:** Поиск по `SearchText` выполняется в памяти (`ToListAsync` + LINQ в памяти) из-за ограничений SQLite с Unicode. Защита — лимит `MaxInMemoryFilterLimit = 5000`. Это сознательный компромисс, задокументированный в комментариях.

- **Вектор атаки:** Потенциальное повышение потребления RAM при одновременном выполнении множества поисковых запросов (DoS).

- **Рекомендация:** Текущая реализация приемлема. Рассмотреть использование FTS5 (Full-Text Search) в SQLite для будущих версий.

- **Ссылки:** CWE-400

---

## Анализ NuGet-зависимостей и CVE

| Пакет | Версия | Статус |
|-------|--------|--------|
| `BCrypt.Net-Next` | 4.2.0 | Без известных CVE |
| `ClosedXML` | 0.105.0 | Без известных CVE (в 0.101.0 был транзитивный CVE-2018-8292 от `System.Net.Http`, в 0.105.0 устранён) |
| `QuestPDF` | 2026.5.0 | Актуальная версия, известных CVE нет |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.8 | Актуальная версия |
| `Serilog` / `Serilog.Sinks.File` | 4.3.1 / 7.0.0 | Актуальные версии |
| `OxyPlot.Wpf` | 2.2.0 | Без критических CVE |
| `Microsoft.Extensions.DependencyInjection` | 10.0.8 | Актуальная версия |

**Вывод:** Все используемые пакеты актуальны, известных критических уязвимостей не обнаружено.

---

## Отсутствие критических уязвимостей (Positive Security)

Ниже перечислены уязвимости/векторы, которые **проверялись, но не обнаружены**:

| Категория | Результат |
|-----------|-----------|
| **BinaryFormatter / SoapFormatter / LosFormatter** | Не используются |
| **SQL-инъекция** | Не обнаружена (все запросы через EF Core LINQ, параметризованы) |
| **FromSqlRaw / ExecuteSqlRaw** | Не используются |
| **Хардкод паролей / токенов / API keys** | Не обнаружен |
| **MD5 / SHA1 / DES / RC2 / RNGCryptoServiceProvider** | Не используются |
| **Режим ECB** | Не используется |
| **System.Random для криптографии** | Не используется |
| **unsafe / fixed / указатели** | Не используются |
| **DllImport / LoadLibrary / Assembly.LoadFrom** | Не используются |
| **Динамическая компиляция (CodeDom)** | Не используется |
| **HttpClient / WebRequest / WebSocket** | Не используются (приложение оффлайн) |
| **WebBrowser / CefSharp / WebView2** | Не используются |
| **Process.Start с command injection** | Не обнаружен |
| **Path Traversal (прямой)** | Не обнаружен (пути из диалогов или контролируемые) |
| **Временные файлы (`GetTempPath`)** | Не используются |
| **Запись в реестр** | Не обнаружена |
| **Stack trace в UI** | Не выводится (только `ex.Message`) |

---

## Топ-5 наиболее критичных проблем (приоритет исправления)

1. **[HIGH]** **Пароли в памяти как `string`** — Перейти на `SecureString` или `char[]` с обнулением для всех password-полей в ViewModel.
2. **[HIGH]** **Слабая политика паролей** — Увеличить минимальную длину до 10–12 символов, добавить проверку сложности и deny-list.
3. **[HIGH]** **BCrypt work factor** — Явно задать `GenerateSalt(12)` или выше.
4. **[HIGH]** **Нормализация ответа через `ToLower()`** — Заменить на `ToLowerInvariant()` или убрать нормализацию регистра.
5. **[MEDIUM]** **Валидация ввода в UI** — Включить `ValidatesOnNotifyDataErrors`, добавить `ValidationRules` и `MaxLength` для всех TextBox.

---

## Общие рекомендации по архитектуре безопасности

### 1. Defence in Depth
- Не полагаться только на «приложение оффлайн». Добавить **шифрование SQLite-файла** через `SQLCipher` или `Microsoft.Data.Sqlite` с шифрованием на уровне файла. Финансовые данные в незашифрованном SQLite-файле доступны любому, у кого есть доступ к файлу БД.
- Добавить **контроль целостности** резервных копий (checksum при создании).

### 2. Принцип наименьших привилегий (Least Privilege)
- Убедиться, что приложение **не требует прав администратора**. В текущей реализации это так, но явный манифест (`app.manifest`) устранит неопределённость.
- Ограничить ACL для папок `Exports` и бэкапов только текущим пользователем.

### 3. Разделение обязанностей (Separation of Duties)
- В текущей реализации используется **единый пароль** для всего приложения. Для многопользовательского сценария (если планируется) необходима система ролей (RBAC) с разделением прав: администратор, бухгалтер, оператор.

### 4. Безопасность данных в покое (Data at Rest)
- Рассмотреть возможность шифрования поля `Description` в `Transaction` для чувствительных комментариев.
- Хеши паролей (`PasswordHash`, `SecurityAnswerHash`) хранятся корректно (BCrypt), но сам файл БД не защищён.

### 5. Безопасность логов
- Добавить политику ротации и **ограничения доступа** к лог-файлам. В текущей реализации логи пишутся в папку приложения, доступную другим пользователям системы.

---

## Чек-лист для повторного аудита после исправлений

- [ ] Все password-поля в ViewModel используют `SecureString` или `char[]` с `Array.Clear()`
- [ ] `PasswordBox.SecurePassword` используется вместо `PasswordBox.Password` в code-behind
- [ ] Минимальная длина пароля ≥ 10 символов
- [ ] BCrypt использует `GenerateSalt(12)` или выше
- [ ] `ToLowerInvariant()` используется вместо `ToLower()` для нормализации
- [ ] Все TextBox в `TransactionEditView` имеют `ValidatesOnNotifyDataErrors=True`
- [ ] Все числовые поля в `SettingsView` имеют `ValidationRules` и `MaxLength`
- [ ] `SanitizeBackupPath` применяется в `ApplyPropertiesToSettings` / `SaveSettingsAsync`
- [ ] `CleanupOldBackups` валидирует путь через `SanitizeBackupPath`
- [ ] `Process.Start` для открытия файлов проверяет расширение по whitelist
- [ ] `Environment.ProcessPath` проверяется на `null` и существование файла
- [ ] Добавлен `app.manifest` с `requestedExecutionLevel`
- [ ] Лог-файлы защищены ACL (доступ только текущему пользователю)
- [ ] Рассмотрено использование `SqliteConnectionStringBuilder` вместо интерполяции
- [ ] Все категории полей ввода имеют `MaxLength` в XAML
- [ ] Проведён регрессионный прогон unit-тестов (`dotnet test`)

---

## Заключение

Приложение AutoKassa имеет **прочную архитектурную основу** с корректным использованием EF Core, BCrypt и MVVM. Основные риски связаны не с архитектурными уязвимостями, а с **деталями реализации**: хранением паролей в памяти, слабой политикой паролей и недостаточной валидацией UI. Исправление топ-5 проблем существенно повысит общий уровень безопасности приложения.
