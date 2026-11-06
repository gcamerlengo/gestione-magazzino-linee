# Valori comuni agli script dell'installazione. Solo caratteri ASCII: Windows PowerShell 5.1.
$ErrorActionPreference = 'Stop'
$Dati        = Join-Path $env:ProgramData 'MagazzinoRicambi'
$PgData      = Join-Path $Dati 'database'
$Impostazioni = Join-Path $Dati 'impostazioni.json'
$Credenziali = Join-Path $Dati 'credenziali-database.txt'
$CartellaBackup = Join-Path $Dati 'backup'
$Log         = Join-Path $Dati 'log'
$ServizioDb  = 'MagazzinoDatabase'
$ServizioApp = 'MagazzinoRicambi'
$PortaDb     = 5439
$PortaApp    = 5080

function Scrivi([string]$testo) {
    $riga = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $testo
    Write-Host $riga
    if (Test-Path $Log) { Add-Content -Path (Join-Path $Log 'installazione.log') -Value $riga }
}

# Esegue un programma esterno: l'output va nel registro, un codice di uscita diverso da 0 ferma tutto.
function Esegui([string]$exe, [string[]]$argomenti, [switch]$Tollera) {
    $prec = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $exe @argomenti 2>&1 | ForEach-Object { Scrivi "  $_" } } finally { $ErrorActionPreference = $prec }
    if ($LASTEXITCODE -ne 0 -and -not $Tollera) { throw "$([IO.Path]::GetFileName($exe)) non riuscito (codice $LASTEXITCODE)" }
    $LASTEXITCODE
}

function NuovaPassword([int]$lunghezza = 32) {
    $caratteri = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'.ToCharArray()
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $buf = New-Object byte[] $lunghezza
    $rng.GetBytes($buf)
    -join ($buf | ForEach-Object { $caratteri[$_ % $caratteri.Length] })
}

function LeggiImpostazioni {
    if (Test-Path $Impostazioni) { Get-Content $Impostazioni -Raw | ConvertFrom-Json } else { $null }
}

function IndirizzoRete {
    # Primo indirizzo IPv4 di una scheda di rete attiva con gateway (la rete aziendale), non 169.254 o loopback.
    $ip = Get-NetIPConfiguration -ErrorAction SilentlyContinue |
        Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
        ForEach-Object { $_.IPv4Address.IPAddress } | Select-Object -First 1
    if (-not $ip) { $ip = 'localhost' }
    $ip
}

function AttendiProgramma([int]$secondi = 120) {
    $fine = (Get-Date).AddSeconds($secondi)
    while ((Get-Date) -lt $fine) {
        try {
            $r = Invoke-WebRequest -Uri "http://localhost:$PortaApp/accesso" -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -eq 200) { return $true }
        } catch { Start-Sleep -Seconds 2 }
    }
    $false
}
