# DbMigration — строгий код-ревью (`docs/REVIEW.md`)

> Ревизия по `docs/SPEC.md`. Проверены `src/DbMigration.Core/**`, `src/DbMigration.App/**`,
> `tests/DbMigration.Tests/**`, проектные файлы и `docs/**`.
> Проверено фактически: `dotnet build DbMigration.slnx -warnaserror` — **0 предупреждений, 0 ошибок**;
> `dotnet test` — **92/92 пройдено**. Никакие исходники в ходе ревью не изменялись.

## Вердикт

Код в целом зрелый: чистая архитектура (Core без WinForms, UI только через контракты),
единый стиль, корректные `async`/`using`, аккуратная тёмная тема, осмысленные тесты и реально
проходящие DoD #1–#2. Комментариев в коде нет, пользовательские строки — на русском,
тема соответствует SPEC §2.

Однако есть **один опасный дефект целостности данных** (молчаливое усечение резервной копии),
несколько заметных функциональных/спек-несоответствий (F3 «вставка строки», F9 «папка резервных
копий», настройка облачного узла не применяется, сверка counts для не-`public` схем),
несколько мест с потенциально необработанными исключениями в `async void`/fire-and-forget и ряд
мелких замечаний. Блокеров сборки/тестов нет — **открытых критичных блокеров, кроме п.1, нет**.

Итого: **24 находки** (1 Критично / 8 Важно / 10 Желательно / 5 Мелочь).

---

## 0. Статус после финальной правки

Исправлено в коде: **п.1** (резервная копия больше не усекается), **п.2** (сверка counts
использует `QualifiedName`), **п.3** и **п.4** (`async void`/fire-and-forget обёрнуты в
`try/catch` с журналированием), **п.6** (папка резервных копий применяется и сохраняется),
**п.10** (утечка `ContextMenuStrip`), **п.11** (`Clipboard` c перехватом `ExternalException`),
**п.12** (`IsInputKey`), **п.16** (мёртвый тернарник), **п.17** (русский дефолт статус-бара).

Остаются открытыми: **п.5, п.7, п.8, п.9, п.13–15, п.18–24** (см. ниже).

---

## 1. Находки

### Критично

**1. Резервное копирование молча усекает таблицы по лимиту SQL-консоли.**
`src/DbMigration.App/Views/BackupView.cs:217`
```csharp
await State.Services.Exporter.ExportTableAsync(profile, schema, format, stream, _includeData.Checked, State.Settings.MaxQueryRows);
```
`MaxQueryRows` — это лимит строк **SQL-консоли** (по умолчанию 5000, клампится до 250000 в
`SettingsView.Save`). В `DataExporter.WriteRowsAsync` (`src/DbMigration.Core/Export/DataExporter.cs:210`)
цикл `if (maxRows > 0 && count >= maxRows) break;` прекращает чтение. То есть для таблицы с числом
строк больше 5000 «резервная копия» будет **неполной и без предупреждения**; статус при этом
рапортует «Сохранено файлов: N».
**Исправление:** для резервного копирования передавать безлимит (`maxRows = 0`) или отдельную
настройку/чекбокс «Все строки», и явно логировать/предупреждать при усечении (как это уже делает
SQL-консоль через `QueryResult.Truncated`).

### Важно

**2. Сверка количества строк после миграции неверна для схем, отличных от схемы по умолчанию.**
`src/DbMigration.App/Views/MigrationView.cs:400-405` (и `src/DbMigration.Core/Migration/MigrationEngine.cs:196`)
Список таблиц приходит из `MigrationEngine.GetTableNamesAsync`, который возвращает `t.Name` —
**без схемы**. Затем `GetRowCountAsync(source, table)` (`DatabaseProviderBase.cs:281`) выполняет
`"SELECT COUNT(*) FROM " + QuoteQualifiedName(table)`. Для PostgreSQL/SQL Server с таблицей в
не-`public`/не-`dbo` схеме имя без схемы резолвится иначе → `COUNT(*)` падает, исключение ловится и
строка сверки молча становится «сверка недоступна». Миграция при этом использует `table.QualifiedName`
и проходит корректно — расходится именно сверка.
**Исправление:** возвращать/переносить `QualifiedName` (или пары schema+name) для сверки, а не
голое `Name`.

**3. `async void` обработчик без перехвата исключений.**
`src/DbMigration.App/Views/ConnectionsView.cs:199`
```csharp
private async void AddProfile()
{
    using var dialog = new ConnectionEditorDialog();
    if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Result is { } profile)
    {
        await State.UpsertProfileAsync(profile);   // без try/catch
        Reload();
    }
}
```
SPEC §6: «No `async void` except WinForms event handlers, **which must catch and surface errors**».
`UpsertProfileAsync` → `SaveProfilesAsync` пишет на диск; при исключении (нет прав, диск, DPAPI)
оно станет необработанным и уронит приложение. То же по сути и у лямбд-обработчиков
`ConnectionsView.cs:52` (`await DeleteSelectedAsync()` — без перехвата в `RemoveProfileAsync`) и
`ConnectionsView.cs:219` (`_ = SaveEditedAsync(edited)` — fire-and-forget без перехвата).
**Исправление:** обернуть await-часть в `try/catch` и показывать `ThemedMessageBox.Error` + `State.Log`.

**4. Fire-and-forget без наблюдения за исключением.**
`src/DbMigration.App/Forms/MainForm.cs:205` (`_ = view.ActivateAsync();`) и
`src/DbMigration.App/Forms/MainForm.cs:231` (`_ = _state.SaveProfilesAsync()`),
`src/DbMigration.App/Views/SqlConsoleView.cs:29` (`_ = ExecuteAsync();`).
Сейчас все `ActivateAsync` возвращают `Task.CompletedTask`, но контракт `ViewBase.ActivateAsync`
позволяет асинхронную работу — при её появлении исключение будет потеряно. Для
`SaveProfilesAsync` это прямая запись на диск.
**Исправление:** сделать `async void`-обёртку с `try/catch` (для обработчиков) либо явно
логировать нештатное завершение (`ContinueWith`/`try`).

**5. Настройка «Облачный узел» в UI не влияет ни на что без перезапуска.**
`src/DbMigration.Core/CoreServices.cs:34` создаёт `new StubCloudStorage(settings.Current.CloudEndpoint)`
один раз при старте; `StubCloudStorage.Endpoint` — `get-only` (`StubCloudStorage.cs:16`).
`SettingsView.Save` (`SettingsView.cs:196`) сохраняет `CloudEndpoint`, но сервис уже зафиксирован.
**Исправление:** пересоздавать/обновлять облачный сервис при сохранении настроек либо явно
помечать в UI «применится после перезапуска» (лучше — первое).

**6. Настройка «Папка резервных копий» (F9) сохраняется, но не используется.**
`AppSettings.BackupFolder` (`src/DbMigration.Core/Models/AppSettings.cs:15`) читается/пишется только в
`SettingsView.cs:168,195`. `BackupView.ExportToFolderAsync` (`BackupView.cs:190`) всегда открывает
`FolderBrowserDialog`, стартуя не из этой папки, и ничего из настройки не берёт.
SPEC §1 F9 требует «backup folder; persisted» — настройка есть, применения нет.
**Исправление:** использовать `State.Settings.BackupFolder` как стартовый `SelectedPath` и/или как
папку по умолчанию.

**7. F3 «вставка строки» не реализована.**
`src/DbMigration.App/Views/DataView.cs` (кнопки: «Режим правки», «Сохранить», «Удалить строку» — вставки нет),
`src/DbMigration.App/Controls/ThemedDataGridView.cs:16` `AllowUserToAddRows = false`.
SPEC §1 F3: «edit cell, **insert row**, delete row; refresh». `SaveChangesAsync` умеет только
`UPDATE` (`DataView.cs:229`), вставка строк отсутствует.
**Исправление:** добавить вставку новой строки (диалог или строка-заготовка) и `INSERT` с
бэкендом по PK; либо зафиксировать изменение объёма F3 отдельным решением.

**8. Блокирующий sync-over-async при старте.**
`src/DbMigration.App/Program.cs:16`
```csharp
state.LoadProfilesAsync().GetAwaiter().GetResult();
```
SPEC §6: «UI must never block», «no `.Result`/`.Wait()`». `GetAwaiter().GetResult()` — тот же
синхронный блок. Загрузка профилей в этот момент — I/O (файл + DPAPI).
**Исправление:** перенести первичную загрузку в асинхронный контекст UI (например, `Load`-событие
главной формы с `async void` и `try/catch`), либо хотя бы сделать метод `async` и запускать через
`Application.Run` без блокировки.

**9. Растущий запрос постраничного просмотра (O(n²) по объёму выгрузки).**
`src/DbMigration.App/Views/DataView.cs:161-166`
```csharp
int take = (_page + 1) * pageSize;
QueryResult result = await provider.ExecuteQueryAsync(profile, $"SELECT * FROM {target}", take);
...
_rows = all.Skip(_page * pageSize).Take(pageSize).ToList();
```
На каждую страницу заново вычитывается `(page+1)*pageSize` строк с сервера, лишние отбрасываются.
При листании больших таблиц это квадратичный трафик/память.
**Исправление:** LIMIT/OFFSET/keyset-пагинация на стороне БД (или кэш текущей страницы),
передавать `pageSize` и признак страницы, а не растущий `take`.

### Желательно

**10. Утечка `ContextMenuStrip` в выпадающем списке.**
`src/DbMigration.App/Controls/ThemedComboBox.cs:185` — при каждом `ShowMenu()` создаётся новый
`ContextMenuStrip`, который нигде не `Dispose`ится (в `Closed` только сбрасывается флаг `_open`).
**Исправление:** хранить один экземпляр как поле и переиспользовать; либо `Dispose` в
`menu.Closed`.

**11. `Clipboard.SetText` без перехвата `ExternalException`.**
`src/DbMigration.App/Views/SchemaView.cs:33` — `Clipboard.SetText(_script.Text)` может бросить
`ExternalException`, если буфер обмена заблокирован другим процессом; это необработанное
исключение прямо в обработчике клика. Рядом `ExportSelected` (`SchemaView.cs:184`) — `async void`
с корректным `try/catch`, здесь же перехвата нет.
**Исправление:** обернуть в `try/catch (ExternalException)` с уведомлением.

**12. `IsInputKey` у однострочного `ThemedTextBox` глушит `Tab`/`Enter`.**
`src/DbMigration.App/Controls/ThemedTextBox.cs:241`
```csharp
protected override bool IsInputKey(Keys keyData) => _editor.Multiline ? base.IsInputKey(keyData) : keyData is Keys.Enter or Keys.Tab || base.IsInputKey(keyData);
```
Фокус обычно на внутреннем `_editor`, поэтому переопределение контейнера в норме не срабатывает —
но это мёртвая/вводящая в заблуждение логика: если контейнер получит фокус (клик по рамке),
`Tab` и `Enter` перестанут выполнять навигацию и не сработает `AcceptButton`.
**Исправление:** убрать переопределение для однострочного режима (оставить `base`).

**13. Мнемоники/доступность из SPEC §2 не реализованы.**
По всему `src/DbMigration.App/**` нет ни одного `&`-акселератора в подписях (проверено поиском),
а также нет `AccessibleName`/`AccessibleRole`. SPEC §2: «accessible (Tab order, mnemonics, contrast)».
**Исправление:** расставить мнемоники на ключевых кнопках/полях и задать доступные имена для
кастомных контролов (`ThemedButton`, `ThemedComboBox`, `ThemedTextBox` и т.д.).

**14. Подавление nullability через `!` (SPEC §6).**
`src/DbMigration.App/Dialogs/ConnectionEditorDialog.cs:222` (`profile.Host!.Length`, `profile.Database!.Length`),
`src/DbMigration.App/Views/DataView.cs:316` (`value.ToString()!`),
`src/DbMigration.Core/Providers/DatabaseProviderFactory.cs:47` (`provider = default!;`).
SPEC §6: «no `!` to silence real nullability, use `?? throw`». В первых двух случаях значения
гарантированно не null после `.Trim()`, но правило формально нарушено.
**Исправление:** `?? string.Empty` / локальные не-null переменные; для `out`-параметра —
`provider = null!` в фабричном паттерне остаётся допустимым исключением, но лучше
`[NotNullWhen(true)]` (см. `TryGet`).

**15. Шрифты создаются на каждый экземпляр и не освобождаются.**
`src/DbMigration.App/Controls/ThemedForm.cs:54` (`AppFonts.Display(15f)`) и
`src/DbMigration.App/Controls/GlowLabel.cs:31` (`AppFonts.Display(14f)`) вызывают `AppFonts.Create`,
создавая новый `Font` на каждый объект. `UiFonts` кеширует свои шрифты корректно, но эти два места —
нет; WinForms не диспозит `Font`, присвоенный контролу.
**Исправление:** добавить кешируемый `UiFonts.Title`/`UiFonts.Display14` (или хранить и
диспозить в `Dispose`).

**16. Мёртвый тернарный оператор (след «ИИ»).**
`src/DbMigration.App/Controls/ThemedForm.cs:166-167`
```csharp
bool maximized = WindowState == FormWindowState.Maximized;
int right = maximized ? Width : Width;
```
Обе ветви идентичны — `maximized` вычисляется впустую.
**Исправление:** `int right = Width;` и удалить локальную переменную.

**17. Английская строка по умолчанию в UI-контроле (правило русского текста).**
`src/DbMigration.App/Controls/StatusBarControl.cs:9` `private string _status = "Ready";`
и `:30` `[DefaultValue("Ready")]`. На практике `MainForm` (`MainForm.cs:34`) всегда перекрывает на
`"Готово"`, поэтому визуально правило не нарушается, но дефолт всё же английский.
**Исправление:** `"Готово"` в качестве значения по умолчанию.

**18. `LastUsedUtc` отображается, но никогда не обновляется при использовании.**
`src/DbMigration.Core/Models/ConnectionProfile.cs:34` (значение по умолчанию — момент создания) и
`src/DbMigration.App/Views/ConnectionsView.cs:162` («Последнее использование»). При реальном
подключении/миграции/проверке дата не меняется.
**Исправление:** обновлять `LastUsedUtc` в точках использования (тест связи, миграция, экспорт) и
сохранять профиль.

**19. Отчёт о миграции использует текущий выбор, а не фактический запуск.**
`src/DbMigration.App/Views/MigrationView.cs:459-461` — в Markdown попадают `SourceProfile?.Name` /
`TargetProfile?.Name`, то есть текущее состояние комбобоксов, которое пользователь мог изменить
после запуска. Плюс `MigrationView.cs:442` определяет JSON по `dialog.FilterIndex == 2`, поэтому
пункт «Все файлы» (индекс 3) сохранит Markdown.
**Исправление:** сохранять источник/цель в момент запуска (в `_lastResult`-контекст) и определять
формат по выбранному расширению/фильтру явно.

### Мелочь

**20. Неиспользуемые члены (мёртвый код).**
- `src/DbMigration.Core/Models/MigrationModels.cs:17` `UnorderedBulkCopy` — нигде не читается
  (движок вставляет построчно через параметры).
- `src/DbMigration.App/Search/FuzzyMatcher.cs:58` `IsMatch` — не вызывается (используется `Score`).
- `src/DbMigration.App/AppState.cs:126` `NotifyProfilesChanged` — не вызывается.
- `src/DbMigration.Core/Models/AppSettings.cs:19` `Clone` — не вызывается.
- `src/DbMigration.Core/Providers/DatabaseProviderBase.cs:239` параметр `batchSize` в
  `ReadTableAsync` не используется (умножение/лимит не применяются).
**Исправление:** удалить или задействовать; для `batchSize` — либо применить, либо убрать из
сигнатуры контракта (контракт заморожен — согласовать).

**21. `ThemedComboBox` переиспользует `ToolStripMenuItem`-подписи с префиксами `✓`/пробелы** —
`ThemedComboBox.cs:198`. Косметика; при локализации/выравнивании лучше отрисовывать маркер, а не
подменять текст.

**22. `Directory.Build.props` ослабляет DoD #1.**
`Directory.Build.props:8` `TreatWarningsAsErrors=false`, тогда как SPEC §6 ожидает чистый
`-warnaserror`. Фактически сборка с флагом проходит (проверено), но настройка не подстраховывает.
**Исправление:** включить `TreatWarningsAsErrors=true` для Core/tests (или на CI передавать флаг).

**23. Устаревший текст документа.**
`docs/IDEAS-VERDICT.md:18-20` утверждает, что `CoreServices.CreateDefault` бросает
`NotImplementedException`, а App содержит только `MainForm` + Theme/Controls. Сейчас Core полностью
реализован, а в App — все экраны. Формулировка помечена «на момент оценки», но вводит в заблуждение.
**Исправление:** добавить пометку «историческая оценка, см. текущее состояние».

**24. Мелочи в `DataView`.**
`DataView.cs:178` `_next.Enabled = _rows.Count >= pageSize` — при точном кратном числе строк
включает пустую последнюю страницу. `DataView.cs:216` сравнение `current?.ToString() != original?.ToString()`
ненадёжно для `byte[]`/`DateTime` (сравнение по строковому представлению типа).
**Исправление:** определять последнюю страницу по фактическому числу строк; для `byte[]`
использовать `SequenceEqual`, для дат — сравнение значений.

---

## 2. Что проверено и нарушений не найдено

- **Комментарии (правило 1):** в `.cs` исходниках под `src/**`, `tests/**` нет ни `//`, ни `/* */`,
  ни `///`. Комментарии есть только в автогенерированных файлах `obj/**` (не исходники). В
  `SqlGuardTests.cs:92` `/* */` — часть строкового SQL-литерала (разрешено). В сгенерированном SQL
  (`DataExporter.cs:75`, `MigrationEngine.cs:128`) и в `publish.ps1:1` (`#Requires`) — тоже разрешено.
  HTML-комментариев `<!--` в `docs/**` нет.
- **Русский UI:** весь пользовательский текст русский; единственное исключение — дефолт
  `StatusBarControl` (см. п.17), который всегда перезаписывается.
- **Тема/визуал (правило 3):** палитра `OpencodeTheme` точно совпадает с SPEC §2; «Автор» —
  `SkyBlue` (светло-синий), GitHub-ссылка — шиммер `PurpleDark`↔`PurpleLight` с `Animate=true`,
  важные слова окрашены семантическими цветами.
- **Замораживание контрактов:** сигнатуры `Models/**`, `Abstractions/**`, `DbMigrationServices`
  соблюдены; App зависит только от контрактов.
- **`ConfigureAwait`:** в Core — присутствует повсюду, в App UI — отсутствует (проверено поиском) —
  точно по SPEC §6.
- **Диспозинг:** `await using` для соединений/команд/читателей; `using` для диалогов и кистей/перьев;
  `LogsView` корректно отписывается от `Activity.EntryAdded` в `Dispose(bool)`; анимации
  отписываются в `OnHandleDestroyed`.
- **Docking/порядок Z-order:** корректный в `ThemedForm`, `MainForm`, `MigrationView`, `SettingsView`,
  `DataView`, `BackupView`, `SchemaView` (проверено по порядку добавления).
- **Дублирование/скаффолдинг:** нет `Form1.cs`, нет `TODO/FIXME/HACK/XXX`, нет пустых заготовок.
- **Тесты (92):** осмысленные проверки — реальный SQLite end-to-end (`MigrationEngineTests`),
  DPAPI round-trip, скрытие пароля на диске, маппинг типов (`DataRow`), парсер `SqlGuard`
  (включая литералы и комментарии), форматтеры литералов/CSV/JSON, фабрика провайдеров.
  `Assert.IsGreaterThanOrEqualTo(1024, BytesUploaded)` корректен (сигнатура MSTest 4.0:
  первым идёт `lowerBound`). Публичные Core-API, используемые UI, покрыты.
- **Сборка/тесты:** `dotnet build -warnaserror` — 0/0; `dotnet test` — 92/92.
- **Single-file publish:** `build/publish.ps1` корректно задаёт `PublishSingleFile`,
  `IncludeNativeLibrariesForSelfExtract`, `IncludeAllContentForSelfExtract`, `EnableCompressionInSingleFile`,
  `--self-contained`; переименование в `DbMigration.exe` есть (правило 4).

---

## 3. Приоритетный план исправлений

1. **п.1** — убрать усечение резервной копии (потеря данных).
2. **п.3, п.4** — обернуть `async void`/fire-and-forget в `try/catch` с логированием.
3. **п.5, п.6, п.7** — довести F9 (облачный узел, папка) и F3 (вставка строки) до SPEC.
4. **п.2, п.9** — корректная сверка по `QualifiedName` и нормальная пагинация.
5. **п.8** — убрать блокирующий старт.
6. Остальное — по убыванию (Желательно → Мелочь).
