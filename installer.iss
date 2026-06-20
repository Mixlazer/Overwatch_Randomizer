; --- installer.iss ---
[Setup]
AppName=Overwatch Randomizer
AppVersion=1.3
DefaultDirName={autopf}\Overwatch Randomizer
DefaultGroupName=Overwatch Randomizer
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=OverwatchRandomizerSetup
Compression=lzma
SolidCompression=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "dist\OverwatchRandomizer.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Overwatch Randomizer"; Filename: "{app}\OverwatchRandomizer.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\Overwatch Randomizer"; Filename: "{app}\OverwatchRandomizer.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"
