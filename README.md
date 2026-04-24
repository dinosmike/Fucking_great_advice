# Fucking Great Advice

Небольшое Windows-приложение (WPF + трей), которое показывает «советы» (текст) в виде оверлея поверх рабочего стола.  
Советы можно запрашивать вручную из трея или показывать по таймеру/при запуске (в зависимости от настроек).

## Что умеет

- **Трей-иконка**:
  - **ЛКМ** по иконке — запросить совет.
  - **Дабл-клик** — открыть настройки.
- **Оверлей с советом**:
  - Появляется поверх всех окон (Topmost), не блокирует UI.
  - **ЛКМ** по оверлею — закрыть.
  - **ПКМ** по оверлею — открыть настройки (не закрывая оверлей).
- **Таймер**: периодически показывает совет (если включён).
- **При старте**: может показать совет при запуске (если включено).
- **Индикация проблем с сетью**:
  - Если совет не пришёл «сразу», или сеть/API недоступны, в трее кратко показывается «offline»-иконка.
  - Иконка берётся из `FuckingGreatAdvice/Assets/offline.png`.

## Настройки

Настройки хранятся в JSON-файле в профиле пользователя:

- **Путь**: `%LocalAppData%\FuckingGreatAdvice\FuckingGreatAdvice_settings.json`
- **Что там**: включение советов, показ при старте, таймер (вкл/выкл, интервал), язык UI и прочее.

Отдельные параметры оверлея (например, размер шрифта) задаются **в коде** (см. ниже).

## Шрифт

- **Основной шрифт UI** задаётся в теме: `FuckingGreatAdvice/Themes/Dark.xaml`.
- **Шрифт текста совета** берётся из `FuckingGreatAdvice/Models/AdviceSettings.cs` и резолвится через `FuckingGreatAdvice/Services/AdviceFontResolver.cs`.
- В проекте используется встроенный шрифт из `FuckingGreatAdvice/Fonts/Helvetica LT Pro UltraCompressed.otf`.
  - В pack-uri имя семейства шрифта — `Helvetica LT Pro UltCompressed` (это внутреннее имя в OTF).

### Где менять размер шрифта совета

В `FuckingGreatAdvice/Models/AdviceSettings.cs`:

- `FontSizePx` — размер текста совета (WPF pixels).

Оверлей применяет это значение в `FuckingGreatAdvice/AdviceOverlayWindow.xaml.cs`:

- `TipText.FontSize = h.FontSizePx;`

## Сборка и запуск

### Быстро: Release publish + запуск exe

В корне репозитория:

```bat
build-release.bat
```

Батник делает:

- `dotnet publish` (self-contained, single-file) в папку `publish\`
- затем запускает `publish\FuckingGreatAdvice.exe`

### Обычная сборка Release (и автозапуск батника)

В проекте настроено, что после **`dotnet build -c Release`** автоматически вызывается `build-release.bat`.

Файл MSBuild-расширения лежит здесь:

- `FuckingGreatAdvice/Directory.Build.targets`

Отключить автозапуск можно так:

```bat
dotnet build -c Release -p:RunReleaseBatAfterBuild=false
```

## Важные файлы проекта

- **Трей и иконки**
  - `FuckingGreatAdvice/TrayService.cs` — логика трея, краткое отображение offline-иконки.
  - `FuckingGreatAdvice/Services/AppIconFactory.cs` — загрузка `Assets/offline.png` и преобразование в `Icon` для `NotifyIcon`.
- **Советы / сеть / ретраи**
  - `FuckingGreatAdvice/Services/AdviceService.cs` — запрос совета, окно ожидания, быстрый показ ошибки при запросе из трея.
  - `FuckingGreatAdvice/Services/GreatAdviceApiClient.cs` — HTTP-запрос к API.
- **Оверлей**
  - `FuckingGreatAdvice/AdviceOverlayWindow.xaml` / `.xaml.cs` — окно оверлея.
- **Настройки**
  - `FuckingGreatAdvice/SettingsWindow.xaml` / `.xaml.cs` — UI настроек.
  - `FuckingGreatAdvice/Services/SettingsStorage.cs` — чтение/запись JSON настроек.

## Требования

- Windows 10/11
- .NET SDK (для сборки из исходников; целевой фреймворк: `net6.0-windows`)

