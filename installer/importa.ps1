# Importa i dati dal vecchio programma Gestione Manutenzione (file manutenzione.db).
# Prima fa una prova (nessun dato scritto) e mostra il resoconto, poi chiede conferma.
param([Parameter(Mandatory = $true)][string]$Cartella)
. (Join-Path $PSScriptRoot 'comune.ps1')
Add-Type -AssemblyName System.Windows.Forms
$dlg = New-Object System.Windows.Forms.OpenFileDialog
$dlg.Title = 'Scegli il database di Gestione Manutenzione (manutenzione.db)'
$dlg.Filter = 'Database Gestione Manutenzione (*.db)|*.db|Tutti i file|*.*'
if ($dlg.ShowDialog() -ne 'OK') { exit 0 }
$sorgente = $dlg.FileName

# Si lavora su una copia: il file originale (anche se e' sulla rete) non viene toccato.
$copia = Join-Path $env:TEMP ('manutenzione-{0:yyyyMMddHHmmss}.db' -f (Get-Date))
Copy-Item $sorgente $copia
$cs = (LeggiImpostazioni).ConnectionStrings.Magazzino
$exe = Join-Path $Cartella 'importa\Magazzino.Import.exe'
$report = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Magazzino - resoconto importazione.md'

& $exe $copia $cs $report --prova
if ($LASTEXITCODE -ne 0) {
    [System.Windows.Forms.MessageBox]::Show("La prova non e' riuscita. Vedi la finestra per il motivo.", 'Importazione') | Out-Null
    Read-Host 'Premi Invio per chiudere'
    exit 1
}
Start-Process notepad.exe $report
$ok = [System.Windows.Forms.MessageBox]::Show("Prova riuscita: il resoconto e' aperto nel Blocco note.`n`nImportare davvero i dati adesso?", 'Importazione', 'YesNo', 'Question')
if ($ok -ne 'Yes') { exit 0 }
& $exe $copia $cs $report
if ($LASTEXITCODE -eq 0) {
    [System.Windows.Forms.MessageBox]::Show("Importazione completata. Resoconto in: $report", 'Importazione') | Out-Null
} else {
    [System.Windows.Forms.MessageBox]::Show("Importazione non riuscita: nessun dato e' stato scritto. Vedi la finestra per il motivo.", 'Importazione') | Out-Null
    Read-Host 'Premi Invio per chiudere'
}
Remove-Item $copia -ErrorAction SilentlyContinue
