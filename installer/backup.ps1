# Copia completa del database in C:\ProgramData\MagazzinoRicambi\backup. Tiene gli ultimi 30.
param([Parameter(Mandatory = $true)][string]$Cartella)
. (Join-Path $PSScriptRoot 'comune.ps1')
$imp = LeggiImpostazioni
$cs = @{}
$imp.ConnectionStrings.Magazzino -split ';' | ForEach-Object { $k, $v = $_ -split '=', 2; if ($k) { $cs[$k.Trim()] = $v } }
$file = Join-Path $CartellaBackup ('magazzino-{0:yyyyMMdd-HHmm}.backup' -f (Get-Date))
$env:PGPASSWORD = $cs['Password']
$esito = Esegui (Join-Path $Cartella 'pgsql\bin\pg_dump.exe') @('-h', 'localhost', '-p', $cs['Port'], '-U', $cs['Username'], '-F', 'c', '-f', $file, $cs['Database']) -Tollera
Remove-Item Env:\PGPASSWORD
if ($esito -ne 0) { Scrivi "Backup NON riuscito ($esito)"; exit 1 }
Scrivi "Backup salvato: $file"
Get-ChildItem $CartellaBackup -Filter 'magazzino-*.backup' | Sort-Object Name -Descending | Select-Object -Skip 30 | Remove-Item -Force
