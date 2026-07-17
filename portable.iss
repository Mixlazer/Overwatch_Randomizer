[Setup]
AppName=Overwatch Randomizer Portable
AppVersion=2.7
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
AllowCancelDuringInstall=no
OutputDir=modern_app\releases
OutputBaseFilename=OverwatchRandomizer-Portable-x64
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "modern_app\releases\windows\*"; DestDir: "{tmp}\OverwatchRandomizer"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "{tmp}\OverwatchRandomizer\OverwatchRandomizer.Modern.exe"; WorkingDir: "{tmp}\OverwatchRandomizer"; Flags: waituntilterminated
