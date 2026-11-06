; Setup di Windows per "Magazzino ricambi": programma + database PostgreSQL in un unico installatore.
; Si compila con Inno Setup 6:  ISCC.exe /DVersione=1.0.0 /DSorgente=<cartella con programma, importa, pgsql> Magazzino.iss

#ifndef Versione
  #define Versione "0.0.0"
#endif
#ifndef Sorgente
  #define Sorgente "..\out"
#endif

[Setup]
AppId={{6E0B3C55-2B4E-4E59-9F0C-6D2B1C7A1E21}
AppName=Magazzino ricambi
AppVersion={#Versione}
AppPublisher=Olio Dante - Manutenzione
DefaultDirName={autopf}\Magazzino ricambi
DefaultGroupName=Magazzino ricambi
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\out\setup
OutputBaseFilename=Magazzino-ricambi-setup-{#Versione}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Magazzino ricambi
SetupLogging=yes

[Languages]
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"

[Files]
Source: "{#Sorgente}\programma\*"; DestDir: "{app}\programma"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Sorgente}\importa\*"; DestDir: "{app}\importa"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Sorgente}\pgsql\*"; DestDir: "{app}\pgsql"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "*.ps1"; DestDir: "{app}"; Flags: ignoreversion

[INI]
Filename: "{app}\Magazzino ricambi.url"; Section: "InternetShortcut"; Key: "URL"; String: "http://localhost:5080/"

[Icons]
Name: "{commondesktop}\Magazzino ricambi"; Filename: "{app}\Magazzino ricambi.url"; IconFilename: "{app}\programma\Magazzino.Web.exe"
Name: "{group}\Magazzino ricambi"; Filename: "{app}\Magazzino ricambi.url"; IconFilename: "{app}\programma\Magazzino.Web.exe"
Name: "{group}\Importa dati da Gestione Manutenzione"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\importa.ps1"" -Cartella ""{app}"""
Name: "{group}\Backup adesso"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\backup.ps1"" -Cartella ""{app}"""
Name: "{group}\Cartella dati e backup"; Filename: "{commonappdata}\MagazzinoRicambi"

[Run]
Filename: "{app}\Magazzino ricambi.url"; Description: "Apri Magazzino ricambi"; Flags: postinstall shellexec nowait skipifsilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\disinstalla.ps1"" -Cartella ""{app}"""; Flags: runhidden waituntilterminated; RunOnceId: "TogliServizi"

[Code]
// Prima di copiare i file nuovi si fermano i servizi, altrimenti i file sono bloccati.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Codice: Integer;
begin
  // Prima il programma, poi il database (che tiene aperti i file di pgsql). Si aspetta che siano fermi davvero.
  Exec('powershell.exe', '-NoProfile -Command "foreach ($s in ''MagazzinoRicambi'',''MagazzinoDatabase'') { if (Get-Service $s -ErrorAction SilentlyContinue) { Stop-Service $s -Force; (Get-Service $s).WaitForStatus(''Stopped'', ''00:01:00'') } }"',
       '', SW_HIDE, ewWaitUntilTerminated, Codice);
  Sleep(1000);
  Result := '';
end;

// Database e servizi si preparano qui (e non in [Run]) per poter fermare il setup se qualcosa va storto.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Codice: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := 'Preparo database e servizi (puo'' richiedere un minuto)...';
    if not Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\installa.ps1') + '" -Cartella "' + ExpandConstant('{app}') + '"',
                '', SW_HIDE, ewWaitUntilTerminated, Codice) or (Codice <> 0) then
    begin
      SuppressibleMsgBox('Installazione non completata: database o servizi non sono stati preparati (codice ' + IntToStr(Codice) + ').' + #13#10 +
             'Dettagli in C:\ProgramData\MagazzinoRicambi\log\installazione.log', mbError, MB_OK, IDOK);
    end;
  end;
end;
