# Overwatch Randomizer

[English](#english) · [Русский](#русский)

<a id="english"></a>

## English

A cross-platform companion for Overwatch custom games featuring role and hero randomization, a points-based match mode, deterministic counterpick recommendations, local screenshot recognition, and a private AI chat.

> This is an unofficial fan project and is not affiliated with Blizzard Entertainment.

### Downloads

- [Windows Setup](https://github.com/Mixlazer/Overwatch_Randomizer/releases/latest/download/OverwatchRandomizerSetup.exe)
- [Windows x64 portable EXE](https://github.com/Mixlazer/Overwatch_Randomizer/releases/latest/download/OverwatchRandomizer-Portable-x64.exe)
- [Android ARM64 APK](https://github.com/Mixlazer/Overwatch_Randomizer/releases/latest/download/OverwatchRandomizer-arm64-v8a.apk)

The current version is **2.7**. Installers, portable packages, checksums, and release notes are available in [GitHub Releases](https://github.com/Mixlazer/Overwatch_Randomizer/releases).

### Redesigned interface

Version 2.7 moves the application to .NET MAUI and provides one responsive interface for Windows and Android. The restrained dark palette uses orange only for active controls and primary actions, while player and counterpick cards remain readable on narrow screens.

Navigation is divided into four focused tabs: **Random**, **Game**, **Counters**, and **AI Chat**. On phones, the title is shortened to `OW Randomizer`, controls reflow to match the available width, and player management cards can be collapsed.

<table>
  <tr>
    <td align="center"><img src="docs/screenshots/game-mobile.png" alt="Game mode on Android" width="300"></td>
    <td align="center"><img src="docs/screenshots/counterpicks-mobile.png" alt="Counterpick mode on Android" width="300"></td>
  </tr>
  <tr>
    <td align="center"><b>Game</b><br>Players, personal hero pools, and rounds</td>
    <td align="center"><b>Counters</b><br>Five non-overlapping counterpick teams</td>
  </tr>
</table>

### Features

#### Random

- `5v5`, Open Queue `6v6`, Stadium, and custom modes for 1–10 participants;
- a dedicated Stadium hero pool;
- the correct `1 Tank / 2 Damage / 2 Support` role composition in 5v5;
- separate role and hero generation;
- unique heroes within each generated team;
- portrait, name, and role color for every result.

#### Game

- 1–10 named players selected through a stable dropdown;
- a personal five-hero `1-2-2` pool for every player;
- active hero selection from the personal pool;
- standard and Stadium pools;
- collapsible player management cards;
- exact hero purchase for 140 points, current-role reroll for 85, and full-pool reroll for 50;
- one free personal reroll per player each round;
- point transfers between players at a `2:1` conversion rate;
- manual entry for eliminations, objective time, damage, healing, and deaths;
- starting a new round awards points, clears statistics, and restores free rerolls.

Round score formula:

```text
55 + eliminations × 12 + every 10 objective seconds × 10
   + damage / 400 + healing / 400 − deaths × 5
```

#### Screenshots and local recognition

- on Android, **Screenshot** opens the camera;
- on Windows, it opens the system file picker;
- Windows accepts JPG, JPEG, PNG, HEIC, HEIF, WebP, and BMP;
- Android can decode and convert HEIC/HEIF locally before inference;
- the vision model fills player statistics in Game mode;
- Counter mode looks for red enemy rows and ignores blue teammate rows;
- recognition responses are constrained by a strict JSON Schema and remain editable for manual verification.

#### Counterpicks

- manual enemy selection through individual dropdowns;
- automatic enemy-team recognition from a screenshot;
- `5v5`, `Open 6v6`, and `Stadium` modes;
- Bronze, Silver, Gold, Platinum, Diamond, Master, and Grandmaster datasets;
- deterministic scoring based on `data/counterpickgg`;
- role counts appropriate to the selected mode;
- five strongest recommended teams;
- no hero is repeated across the five displayed teams.

#### Local AI

On first launch, the user can select the interface language and one of three options:

1. **Standard AI** — [`unsloth/Qwen3.5-0.8B-GGUF`](https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF), `Qwen3.5-0.8B-Q4_K_M.gguf` weights, approximately 740 MB including the vision projector.
2. **Advanced 2B AI** — [`unsloth/Qwen3.5-2B-GGUF`](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF), `Qwen3.5-2B-Q4_K_M.gguf` weights, approximately 1.95 GB including the vision projector. Recommended only for high-end hardware.
3. **Manual mode** — no model is downloaded and every field remains available for manual input.

Both primary model files use **Q4_K_M** quantization. The vision projector is distributed separately by the model authors as `mmproj-BF16.gguf`; no Q4_K_M projector is published. Downloads are retrieved from Hugging Face, verified by SHA-256, and stored locally.

Inference runs through the bundled `llama.cpp` runtime. Screenshots, chat messages, and conversation history are not sent to a cloud API. The interface does not expose an endpoint or external model-name field.

#### AI chat and acceleration

- a dedicated local chat tab;
- Russian and English interface languages;
- `Auto`, `GPU`, and `CPU` acceleration modes;
- Windows uses Vulkan for GPU inference and automatically falls back to CPU if startup fails;
- Android ARM64 uses the CPU runtime;
- chat history is retained only until the application closes.

### System requirements

#### Windows

- Windows 10 version 1809 or later;
- a 64-bit processor;
- approximately 300 MB for the application plus storage for the selected model;
- a compatible Vulkan driver for GPU inference.

The installer does not require administrator privileges and creates an optional shortcut only for the current user. The portable edition is one EXE: it temporarily extracts the required files, runs the application, and removes the temporary session after the application closes.

#### Android

- Android 6.0 or later;
- an ARM64 device;
- a camera is optional but required for direct capture inside the application;
- enough free storage for the APK and selected model;
- a high-end device with ample memory is recommended for the 2B model.

### Building from source

The primary application is located in `modern_app` and uses .NET MAUI 10. Building requires .NET 10 with the `maui-windows` and `maui-android` workloads, Android SDK API 36, JDK 21, and Inno Setup 6 for the Windows installer.

```powershell
.\modern_app\build.ps1
```

Build only one platform:

```powershell
.\modern_app\build.ps1 -SkipAndroid
.\modern_app\build.ps1 -SkipWindows
```

Create the Windows installer and the single-file portable launcher after the Windows build:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\installer.iss
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\portable.iss
```

Run the rules check without building the interface:

```powershell
& "$env:USERPROFILE\.dotnet-maui\dotnet.exe" run `
  --project .\modern_app\Checks\Checks.csproj -c Release
```

The check covers role composition, hero uniqueness, the Stadium pool, purchases, point transfers, round scoring, personal rerolls, the VLM parser contract, and deterministic counterpick recommendations.

### Repository layout

```text
modern_app/             .NET MAUI application for Windows and Android
data/counterpickgg/     rank-specific counterpick strength data
docs/screenshots/       screenshots of the current interface
installer.iss           Windows Setup definition
portable.iss            single-file portable launcher definition
OverwatchRandomizerSetup.exe
                        installer for the current version
main.py                 preserved classic Tkinter version
```

### Privacy

- no cloud AI server is used;
- models run entirely on the device;
- chat history is not persisted after the application closes;
- Android signing keys and downloaded models are excluded from Git.

### Licenses and sources

- Qwen3.5 GGUF models: [Unsloth](https://huggingface.co/unsloth);
- local inference runtime: [llama.cpp](https://github.com/ggml-org/llama.cpp);
- counterpick data is stored in the local `data/counterpickgg` dataset.

Overwatch and related names are trademarks of Blizzard Entertainment.

---

<a id="русский"></a>

## Русский

Кроссплатформенный помощник для кастомных игр Overwatch: рандомизация ролей и героев, игровой режим с очками, подбор контрпиков, распознавание скриншотов локальной VLM и отдельный ИИ-чат.

> Неофициальный фанатский проект. Не связан с Blizzard Entertainment.

## Скачать

- [Windows Setup](https://github.com/Mixlazer/Overwatch_Randomizer/releases/latest/download/OverwatchRandomizerSetup.exe)
- [Windows x64 — portable EXE](https://github.com/Mixlazer/Overwatch_Randomizer/releases/latest/download/OverwatchRandomizer-Portable-x64.exe)
- [Android ARM64 APK](https://github.com/Mixlazer/Overwatch_Randomizer/releases/latest/download/OverwatchRandomizer-arm64-v8a.apk)

Актуальная версия — **2.7**. Готовые файлы и список изменений находятся в [GitHub Releases](https://github.com/Mixlazer/Overwatch_Randomizer/releases).

## Новый интерфейс

Версия 2.7 полностью переносит приложение на .NET MAUI и использует единый адаптивный интерфейс на Windows и Android. Тёмная палитра стала спокойнее, оранжевый цвет используется только для активных элементов и основных действий, а карточки игроков и контрпиков сохраняют читаемость на узком экране.

Основная навигация разделена на четыре вкладки: **Рандом**, **Игра**, **Контры** и **ИИ-чат**. На телефоне длинное название сокращается до `OW Randomizer`, элементы управления перестраиваются под ширину экрана, а карточки игроков можно сворачивать.

<table>
  <tr>
    <td align="center"><img src="docs/screenshots/game-mobile.png" alt="Игровой режим на Android" width="300"></td>
    <td align="center"><img src="docs/screenshots/counterpicks-mobile.png" alt="Контрпики на Android" width="300"></td>
  </tr>
  <tr>
    <td align="center"><b>Игра</b><br>Игроки, личные наборы героев и раунды</td>
    <td align="center"><b>Контры</b><br>Топ-5 составов без повторения героев</td>
  </tr>
</table>

## Возможности

### Рандом

- режимы `5v5`, открытая очередь `6v6`, Stadium и своя игра на 1–10 участников;
- отдельный пул героев Stadium;
- корректный состав ролей `1 танк / 2 урона / 2 поддержки` для 5v5;
- раздельная генерация ролей и персонажей;
- уникальные герои внутри сгенерированной команды;
- портрет, имя и цвет роли для каждого результата.

### Игра

- от 1 до 10 именованных игроков, количество выбирается стабильным выпадающим списком;
- каждому игроку выдаётся личный набор из пяти уникальных героев состава `1-2-2`;
- выбор активного героя из личного набора;
- обычный или Stadium-пул;
- сворачиваемые карточки управления игроками;
- точный герой за 140 очков, реролл текущей роли за 85 и полный реролл набора за 50;
- один бесплатный личный реролл на игрока в каждом раунде;
- передача очков другому игроку по правилу `2:1`;
- ручной ввод устранений, времени на объекте, урона, лечения и смертей;
- новый раунд начисляет очки, очищает статистику и восстанавливает бесплатные рероллы.

Формула очков раунда:

```text
55 + устранения × 12 + каждые 10 секунд на объекте × 10
   + урон / 400 + лечение / 400 − смерти × 5
```

### Скриншоты и локальное распознавание

- на Android кнопка **Скриншот** открывает камеру;
- на Windows открывается системный проводник;
- Windows принимает JPG, JPEG, PNG, HEIC, HEIF, WebP и BMP;
- Android умеет декодировать и преобразовывать HEIC/HEIF перед инференсом;
- VLM заполняет статистику игроков в режиме игры;
- во вкладке контрпиков VLM ищет красные строки противников и игнорирует синие строки союзников;
- ответы распознавания ограничены строгой JSON Schema, после чего значения всё равно остаются доступными для ручной проверки.

### Контрпики

- ручной выбор противников через отдельные выпадающие списки;
- автоматическое распознавание состава противника со скриншота;
- режимы `5v5`, `Open 6v6` и `Stadium`;
- данные для рангов Bronze, Silver, Gold, Platinum, Diamond, Master и Grandmaster;
- очки контрпиков рассчитываются детерминированно по данным `data/counterpickgg`;
- соблюдается требуемое количество героев каждой роли;
- показываются пять сильнейших составов;
- один герой не повторяется между предложенными составами топ-5.

### Локальный ИИ

При первом запуске можно выбрать язык и один из трёх вариантов:

1. **Обычный ИИ** — [`unsloth/Qwen3.5-0.8B-GGUF`](https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF), основные веса `Qwen3.5-0.8B-Q4_K_M.gguf`, около 740 МБ вместе с vision projector.
2. **Продвинутый ИИ 2B** — [`unsloth/Qwen3.5-2B-GGUF`](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF), основные веса `Qwen3.5-2B-Q4_K_M.gguf`, около 1,95 ГБ вместе с vision projector. Рекомендуется только для топового железа.
3. **Ручной режим** — модель не загружается, все поля заполняются вручную.

Основные веса обеих моделей используют квантизацию **Q4_K_M**. Vision projector поставляется авторами отдельно как `mmproj-BF16.gguf`; для него Q4_K_M-вариант не опубликован. Файлы скачиваются с Hugging Face, проверяются по SHA-256 и хранятся локально.

ИИ работает через встроенный `llama.cpp`. Скриншоты, сообщения и история чата не отправляются в облачный API. В интерфейсе нет endpoint или внешнего имени модели.

### ИИ-чат и ускорение

- отдельная вкладка локального чата;
- переключатель русского и английского языков;
- режимы ускорения `Авто`, `GPU` и `CPU`;
- Windows использует Vulkan для GPU и автоматически откатывается на CPU при ошибке запуска;
- Android ARM64 использует CPU runtime;
- история чата хранится только до закрытия приложения.

## Системные требования

### Windows

- Windows 10 версии 1809 или новее;
- 64-разрядный процессор;
- примерно 300 МБ для приложения плюс место под выбранную модель;
- для GPU-инференса — совместимый Vulkan-драйвер.

Установщик не требует прав администратора и создаёт необязательный ярлык только для текущего пользователя. Portable-версия состоит из одного EXE: она временно извлекает необходимые файлы, запускает приложение и удаляет временную сессию после его закрытия.

### Android

- Android 6.0 или новее;
- устройство ARM64;
- камера необязательна, но нужна для съёмки непосредственно из приложения;
- свободное место под APK и выбранную модель;
- для 2B-модели желательно флагманское устройство с большим объёмом оперативной памяти.

## Сборка из исходников

Основное приложение находится в `modern_app` и использует .NET MAUI 10. Нужны .NET 10 с workloads `maui-windows` и `maui-android`, Android SDK API 36, JDK 21 и Inno Setup 6 для Windows Setup.

```powershell
.\modern_app\build.ps1
```

Сборка одной платформы:

```powershell
.\modern_app\build.ps1 -SkipAndroid
.\modern_app\build.ps1 -SkipWindows
```

После Windows-сборки установщик и однофайловый portable launcher создаются так:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\installer.iss
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\portable.iss
```

Проверки правил без сборки интерфейса:

```powershell
& "$env:USERPROFILE\.dotnet-maui\dotnet.exe" run `
  --project .\modern_app\Checks\Checks.csproj -c Release
```

Сценарий проверяет составы ролей, уникальность героев, Stadium-пул, покупки, переводы очков, формулу раунда, персональные рероллы, контракт VLM-парсера и детерминированный топ-5 контрпиков.

## Структура

```text
modern_app/             приложение .NET MAUI для Windows и Android
data/counterpickgg/     таблицы силы контрпиков по рангам
docs/screenshots/       изображения актуального интерфейса
installer.iss           сценарий Windows Setup
portable.iss            сценарий однофайлового portable launcher
OverwatchRandomizerSetup.exe
                        готовый установщик текущей версии
main.py                 сохранённая классическая Tkinter-версия
```

## Конфиденциальность

- облачный сервер для ИИ не используется;
- модели запускаются только на устройстве;
- чат не сохраняется после закрытия приложения;
- ключи Android-подписи и загруженные модели исключены из Git.

## Лицензии и источники

- Qwen3.5 GGUF: [Unsloth](https://huggingface.co/unsloth);
- локальный inference runtime: [llama.cpp](https://github.com/ggml-org/llama.cpp);
- данные контрпиков подготовлены из локального набора `data/counterpickgg`.

Overwatch и связанные названия принадлежат Blizzard Entertainment.
