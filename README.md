# Overwatch Randomizer

Desktop randomizer for Overwatch roles and heroes.

## Features

- 5v5, Open Queue, Stadium, and Custom modes.
- Separate hero pool for Stadium.
- Updated hero roster from the official Overwatch heroes page.
- Hero avatars.
- Low-priority Windows process mode to reduce impact while Overwatch is running.

## Run From Source

Requires Python 3 with Tkinter.

```powershell
python main.py
```

Quick data check:

```powershell
python main.py --check
```

## Build

Install PyInstaller, then build:

```powershell
python -m pip install pyinstaller
python -m PyInstaller main.spec --clean --noconfirm
```

Create the Windows installer with Inno Setup:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

The installer is written to `OverwatchRandomizerSetup.exe`.

## Notes

This is an unofficial fan tool and is not affiliated with Blizzard Entertainment.
