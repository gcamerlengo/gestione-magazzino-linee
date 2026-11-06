# Eseguito dal setup (come amministratore) dopo aver copiato i file.
# Si puo' rilanciare: alla prima installazione crea il database, agli aggiornamenti lo lascia com'e'.
param([Parameter(Mandatory = $true)][string]$Cartella)
. (Join-Path $PSScriptRoot 'comune.ps1')
trap { Scrivi "ERRORE: $_"; exit 1 }

New-Item -ItemType Directory -Force -Path $Dati, $Log, $CartellaBackup, (Join-Path $Dati 'chiavi') | Out-Null
# Solo SYSTEM e amministratori vedono impostazioni e password; il servizio database scrive nella sua cartella.
& icacls $Dati /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null

$pgBin = Join-Path $Cartella 'pgsql\bin'
$prima = -not (Test-Path (Join-Path $PgData 'PG_VERSION'))

if ($prima) {
    Scrivi 'Creo il database...'
    $pwPostgres = NuovaPassword
    $pwApp = NuovaPassword
    # initdb gira con i diritti ridotti dell'utente che installa: gli serve accesso alla cartella e al file password.
    $io = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    New-Item -ItemType Directory -Force -Path $PgData | Out-Null
    & icacls $PgData /grant "*${io}:(OI)(CI)F" | Out-Null
    $tmp = [IO.Path]::GetTempFileName()
    Set-Content -Path $tmp -Value $pwPostgres -NoNewline -Encoding ascii
    try { Esegui (Join-Path $pgBin 'initdb.exe') @('-D', $PgData, '-U', 'postgres', "--pwfile=$tmp", '-E', 'UTF8', '--no-locale', '-A', 'scram-sha-256') | Out-Null }
    finally { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
    if (-not (Test-Path (Join-Path $PgData 'PG_VERSION'))) { throw 'Creazione del database non riuscita (initdb).' }
    Add-Content -Path (Join-Path $PgData 'postgresql.conf') -Value "`nport = $PortaDb`nlisten_addresses = 'localhost'`n"
    Set-Content -Path $Credenziali -Encoding ascii -Value @(
        'Database del programma Magazzino ricambi. Conservare con cura.',
        "Server: localhost porta $PortaDb",
        "Amministratore database: postgres / $pwPostgres",
        "Utente del programma: magazzino / $pwApp")
}

# Servizio del database (account NetworkService, avvio automatico).
& icacls $PgData /grant '*S-1-5-20:(OI)(CI)M' | Out-Null
& icacls $Log /grant '*S-1-5-20:(OI)(CI)M' | Out-Null
if (-not (Get-Service $ServizioDb -ErrorAction SilentlyContinue)) {
    Scrivi 'Registro il servizio del database...'
    Esegui (Join-Path $pgBin 'pg_ctl.exe') @('register', '-N', $ServizioDb, '-D', $PgData, '-S', 'auto') | Out-Null
}
Start-Service $ServizioDb
(Get-Service $ServizioDb).WaitForStatus('Running', '00:01:00')

if ($prima) {
    Scrivi 'Creo utente e database del programma...'
    $env:PGPASSWORD = $pwPostgres
    $sql = "CREATE ROLE magazzino LOGIN PASSWORD '$pwApp'; CREATE DATABASE magazzino OWNER magazzino ENCODING 'UTF8';"
    $psql = Join-Path $pgBin 'psql.exe'
    for ($i = 0; $i -lt 30; $i++) {
        if ((Esegui $psql @('-h', 'localhost', '-p', "$PortaDb", '-U', 'postgres', '-d', 'postgres', '-c', 'SELECT 1') -Tollera) -eq 0) { break }
        Start-Sleep -Seconds 1
    }
    $sql -split ';' | Where-Object { $_.Trim() } | ForEach-Object {
        Esegui $psql @('-h', 'localhost', '-p', "$PortaDb", '-U', 'postgres', '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-c', $_) | Out-Null
    }
    Remove-Item Env:\PGPASSWORD

    $ip = IndirizzoRete
    $imp = [ordered]@{
        ConnectionStrings = [ordered]@{ Magazzino = "Host=localhost;Port=$PortaDb;Database=magazzino;Username=magazzino;Password=$pwApp" }
        Urls = "http://0.0.0.0:$PortaApp"
        Magazzino = [ordered]@{ IndirizzoPubblico = "http://${ip}:$PortaApp"; CostoOrario = 35 }
    }
    $imp | ConvertTo-Json -Depth 5 | Set-Content -Path $Impostazioni -Encoding utf8
    Scrivi "Indirizzo per telefoni e altri PC: http://${ip}:$PortaApp"
}

# Servizio del programma (LocalSystem, avvio automatico, riparte da solo se si ferma).
$exe = Join-Path $Cartella 'programma\Magazzino.Web.exe'
if (-not (Get-Service $ServizioApp -ErrorAction SilentlyContinue)) {
    Scrivi 'Registro il servizio del programma...'
    New-Service -Name $ServizioApp -BinaryPathName "`"$exe`"" -DisplayName 'Magazzino ricambi' `
        -Description 'Gestione magazzino ricambi e manutenzione linee' -StartupType Automatic -DependsOn $ServizioDb | Out-Null
    Esegui 'sc.exe' @('failure', $ServizioApp, 'reset=', '86400', 'actions=', 'restart/5000/restart/5000/restart/30000') | Out-Null
}
Start-Service $ServizioApp

# Porta aperta per la rete aziendale (dominio e privata), non per le reti pubbliche.
if (-not (Get-NetFirewallRule -DisplayName 'Magazzino ricambi' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'Magazzino ricambi' -Direction Inbound -Protocol TCP -LocalPort $PortaApp `
        -Action Allow -Profile Domain, Private | Out-Null
}

# Backup automatico ogni sera alle 21:00.
$azione = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $Cartella 'backup.ps1')`" -Cartella `"$Cartella`""
$quando = New-ScheduledTaskTrigger -Daily -At 21:00
$chi = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$imp = New-ScheduledTaskSettingsSet -StartWhenAvailable
Register-ScheduledTask -TaskName 'Magazzino ricambi - backup' -Action $azione -Trigger $quando -Principal $chi -Settings $imp -Force | Out-Null

if (AttendiProgramma) { Scrivi 'Programma avviato.' } else { throw "Il programma non risponde su http://localhost:$PortaApp. Vedi il registro eventi di Windows." }
