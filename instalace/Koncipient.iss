; Instalátor Koncipientu (Inno Setup 6). Nesestavuj ručně, spusť instalace\sestav.ps1:
; ten připraví složku vystup\program (program, koncipient-cli a modely OCR) a pak zavolá ISCC.
;
; Instaluje se jen pro přihlášeného uživatele, bez práv správce, do
; %LOCALAPPDATA%\Programs\Koncipient. Spisy v Dokumenty\Koncipient instalace ani
; odinstalace nemaže. Program za běhu nepotřebuje síť, modely jsou přibalené.

#ifndef Verze
  #define Verze "0.1.0"
#endif
#ifndef Zdroj
  #define Zdroj "..\vystup\program"
#endif

[Setup]
AppId={{6B0E5B8A-4C2D-4E1B-9F3A-6A2C1D7E9B10}
AppName=Koncipient
AppVersion={#Verze}
AppPublisher=Koncipient
DefaultDirName={localappdata}\Programs\Koncipient
DefaultGroupName=Koncipient
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\vystup
OutputBaseFilename=Koncipient-instalace-{#Verze}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Koncipient.exe
UninstallDisplayName=Koncipient

[Languages]
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"

[Tasks]
Name: "plocha"; Description: "Zástupce na ploše"; GroupDescription: "Zástupci:"

[Files]
Source: "{#Zdroj}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Koncipient"; Filename: "{app}\Koncipient.exe"
Name: "{group}\Odinstalovat Koncipient"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Koncipient"; Filename: "{app}\Koncipient.exe"; Tasks: plocha

[Run]
Filename: "{app}\Koncipient.exe"; Description: "Spustit Koncipient"; Flags: nowait postinstall skipifsilent
