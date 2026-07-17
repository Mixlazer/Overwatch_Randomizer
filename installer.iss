; --- installer.iss ---
[Setup]
AppName=Overwatch Randomizer
AppVersion=2.7
DefaultDirName={autopf}\Overwatch Randomizer
DefaultGroupName=Overwatch Randomizer
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=OverwatchRandomizerSetup
Compression=lzma
SolidCompression=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "modern_app\releases\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Overwatch Randomizer"; Filename: "{app}\OverwatchRandomizer.Modern.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Overwatch Randomizer"; Filename: "{app}\OverwatchRandomizer.Modern.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"
