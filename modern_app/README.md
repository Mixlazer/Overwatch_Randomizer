# Overwatch Randomizer Modern

[English](#english) · [Русский](#русский)

<a id="english"></a>

## English

The primary cross-platform edition built with .NET MAUI 10. The classic Python/Tkinter edition remains available in the repository root.

### Features

- shared C# application code for Windows x64 and Android ARM64;
- randomizer modes for standard play, Stadium, Open Queue, and custom teams of 1–10;
- Game mode for 1–10 participants, each with a personal five-hero `1-2-2` pool;
- active hero selection by portrait;
- point purchases, rerolls, `2:1` transfers, round statistics, and screenshot import;
- one personal free reroll per player each round;
- local screenshot recognition through `llama.cpp` with JSON Schema structured output;
- deterministic top-five counterpick teams without repeated heroes, using `data/counterpickgg` for 5v5, Open 6v6, and Stadium;
- Russian and English interfaces plus a dedicated local AI chat tab;
- `Auto / GPU / CPU` acceleration: Windows uses Vulkan with automatic CPU fallback, while Android currently uses the CPU runtime.

### Local AI

On first launch, the application offers the standard
[Qwen3.5-0.8B Q4_K_M](https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF) option (~740 MB),
the advanced [Qwen3.5-2B Q4_K_M](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF) option (~1.95 GB),
or a fully manual mode. The advanced model is intended for high-end hardware. Downloads are verified by SHA-256 before the bundled `llama.cpp` runtime starts.

Windows x64 and Android ARM64 runtimes are included in their respective distributions. The interface has no endpoint or external model-name fields. Manual mode performs no download; AI can be enabled later from the chat tab.

### Build

Building requires .NET 10 with the `maui-windows` and `maui-android` workloads, Android SDK API 36, and JDK 21. The build script uses the user-local SDK paths configured for this project.

```powershell
.\modern_app\build.ps1
```

Build only one platform:

```powershell
.\modern_app\build.ps1 -SkipAndroid
.\modern_app\build.ps1 -SkipWindows
```

The script runs the rules check first and then creates:

- `releases/windows/OverwatchRandomizer.Modern.exe` with its required libraries;
- `releases/android/OverwatchRandomizer-arm64-v8a.apk`.

On the first Android build, a local signing key and password are created in `signing/`. Signing files and generated releases are excluded from Git.

### Checks

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet-maui"
& "$env:DOTNET_ROOT\dotnet.exe" run --project .\modern_app\Checks\Checks.csproj -c Release
```

Checks cover the `1-2-2` composition, hero uniqueness, Stadium pool, purchases, point transfers, round scoring, personal rerolls, statistics parsing, and deterministic top-five counterpick teams without repeated heroes.

---

<a id="русский"></a>

## Русский

Основная кроссплатформенная версия на .NET MAUI 10. Классическая Python/Tkinter-версия сохранена в корне репозитория.

## Возможности

- Windows x64 и Android ARM64 из общего C#-кода.
- Рандомайзер для обычного режима, Stadium, Open и Custom 1-10.
- Игра для 1-10 участников с личным набором из пяти героев `1-2-2`.
- Выбор активного героя кликом по портрету.
- Покупаемые рероллы, очки, переводы `2:1`, статистика и импорт скриншота.
- Один личный реролл каждому игроку в каждом раунде.
- Локальный VLM Qwen3.5 через `llama.cpp`: распознавание статистики и команды противника с JSON Schema.
- Детерминированный топ-5 контрпиков без повторения героев между составами по данным `data/counterpickgg` для 5v5, Open 6v6 и Stadium.
- Переключатель RU/EN и отдельная вкладка локального ИИ-чата.
- Переключатель ускорения `Авто / GPU / CPU`: Windows использует Vulkan с автоматическим откатом на CPU; Android использует CPU, пока для конкретного Adreno не добавлен совместимый OpenCL runtime.

## Локальный ИИ

При первом запуске приложение предлагает обычную
[Qwen3.5-0.8B Q4_K_M](https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF) (~740 МБ),
продвинутую [Qwen3.5-2B Q4_K_M](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF) (~1,95 ГБ)
или полностью ручной режим. Продвинутая модель рассчитана на топовое железо. Приложение проверяет SHA-256 и запускает встроенный `llama.cpp`.

Windows x64 и Android ARM64 runtime уже находятся в соответствующих дистрибутивах. Полей endpoint и имени модели в интерфейсе нет. Если пользователь выбирает ручной режим, загрузка не выполняется; ИИ можно позднее включить во вкладке чата.

## Сборка

Требуются .NET 10 с workloads `maui-windows` и `maui-android`, Android SDK API 36 и JDK 21. На текущей машине они установлены в пользовательские каталоги, которые уже указаны в скрипте.

```powershell
.\modern_app\build.ps1
```

Можно собрать только одну платформу:

```powershell
.\modern_app\build.ps1 -SkipAndroid
.\modern_app\build.ps1 -SkipWindows
```

Скрипт сначала запускает проверки правил, затем создаёт:

- `releases/windows/OverwatchRandomizer.Modern.exe` и необходимые библиотеки;
- `releases/android/OverwatchRandomizer-arm64-v8a.apk`.

При первой Android-сборке локальный ключ и пароль создаются в `signing/`. Этот каталог и готовые релизы исключены из Git.

## Проверки

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet-maui"
& "$env:DOTNET_ROOT\dotnet.exe" run --project .\modern_app\Checks\Checks.csproj -c Release
```

Проверяются состав `1-2-2`, уникальность героев, Stadium-пул, покупки, перевод очков, формула раунда, персональные рероллы, парсер статистики и детерминированный топ-5 контрпиков без повторений.
