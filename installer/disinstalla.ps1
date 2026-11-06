# Eseguito dalla disinstallazione: toglie servizi, regola firewall e backup automatico.
# I DATI RESTANO in C:\ProgramData\MagazzinoRicambi (database, backup, impostazioni).
param([Parameter(Mandatory = $true)][string]$Cartella)
. (Join-Path $PSScriptRoot 'comune.ps1')
$ErrorActionPreference = 'Continue'
foreach ($s in $ServizioApp, $ServizioDb) {
    if (Get-Service $s -ErrorAction SilentlyContinue) {
        Stop-Service $s -Force -ErrorAction SilentlyContinue
        & sc.exe delete $s | Out-Null
    }
}
Get-NetFirewallRule -DisplayName 'Magazzino ricambi' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Unregister-ScheduledTask -TaskName 'Magazzino ricambi - backup' -Confirm:$false -ErrorAction SilentlyContinue
