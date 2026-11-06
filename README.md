# Gestione magazzino ricambi e manutenzione linee

Nuovo gestionale per lo stabilimento Olio Dante che sostituisce **Gestione Manutenzione**:
ordini di carico e scarico, giacenze calcolate in automatico dai movimenti, e ogni ricambio
prelevato per una linea registrato come intervento di manutenzione su quella linea.

Usato dall'ufficio da PC (browser) e dai tecnici in magazzino da smartphone e tablet.

## Struttura

| Progetto | Contenuto |
| --- | --- |
| `src/Magazzino.Domain` | Entita': anagrafiche, carico (RDA, ordini fornitore, ricevimenti), scarico (buoni di prelievo, interventi), giacenze e movimenti |
| `src/Magazzino.Infrastructure` | EF Core + PostgreSQL, migrazioni, servizi di dominio |
| `src/Magazzino.Import` | Importatore una tantum da `manutenzione.db` di Gestione Manutenzione, con report |
| `src/Magazzino.Web` | Web app Blazor (.NET 8) per ufficio e tecnici |
| `tests/Magazzino.Tests` | Test dei servizi su SQLite in memoria |

## Regole di base

- La giacenza esiste solo per ricambio e ubicazione (`Giacenze`) ed e' modificata solo da
  `GiacenzeService`, nella stessa transazione del movimento e del documento che lo genera.
- I movimenti non si cancellano: si stornano con un movimento opposto.
- Solo il ricevimento (DDT) carica la merce; l'ordine fornitore aggiorna il proprio stato da solo
  (ricevuto in parte / ricevuto).
- Un prelievo per una linea crea l'intervento su quella linea con i ricambi usati e il loro costo
  medio al momento del prelievo. La macchina e' facoltativa ma deve appartenere alla linea.

## Avvio in sviluppo

Serve .NET 8 SDK e un PostgreSQL raggiungibile.

```bash
dotnet tool restore
# crea utente e database (una volta)
#   create user magazzino password 'cambiami' createdb;  create database magazzino owner magazzino;
dotnet ef database update -p src/Magazzino.Infrastructure
dotnet run --project src/Magazzino.Web
dotnet test
```

La stringa di connessione e' `ConnectionStrings:Magazzino` in `src/Magazzino.Web/appsettings.json`
(in produzione va impostata con variabile d'ambiente `ConnectionStrings__Magazzino`).

## Importazione da Gestione Manutenzione

Si esegue una volta, su un database nuovo e vuoto. Il file `manutenzione.db` viene aperto in sola
lettura; tutto avviene in una transazione (o entra tutto o niente) e alla fine viene scritto un
report in Markdown con i conteggi, le giacenze che non tornavano e cosa non e' stato importato.

```bash
# prova: esegue tutto, scrive il report e annulla (nessun dato salvato)
dotnet run --project src/Magazzino.Import -- "X:\SOFTWARE Aziendali\Gestione Manutenzioni\manutenzione.db" "Host=...;Database=magazzino;Username=...;Password=..." report.md --prova
# importazione vera
dotnet run --project src/Magazzino.Import -- "<manutenzione.db>" "<connessione>" report.md
```

Cosa viene importato: linee, macchine, fornitori (anche quelli scritti solo come testo sul ricambio),
ubicazioni, ricambi, ricambi per macchina, giacenze per ubicazione (come movimenti di apertura),
ordini fornitore con stato ricalcolato, interventi completati con i ricambi usati.
Non ancora: piani preventivi e scadenze aperte (modulo scadenzario), ticket, utenti.

## Pagine per l'ufficio

| Pagina | Cosa si fa |
| --- | --- |
| Cruscotto | Ricambi da riordinare, critici a zero, ordini in ritardo e da approvare, valore magazzino; dalla proposta di riordino si creano gli ordini in bozza (uno per fornitore preferito) |
| Ordini fornitore | Elenco per stato, nuova bozza, righe modificabili solo in bozza, conferma/approvazione, conferma fornitore, chiusura, annullamento |
| Arrivo merce | Dal singolo ordine: numero e data DDT, quantita' arrivate (anche parziali), prezzo e ubicazione per riga |
| Giacenze | Quantita' per ubicazione con valore, spostamento tra ubicazioni |
| Movimenti | Ultimi movimenti con filtro per tipo, storno dei trasferimenti e delle rettifiche |
| Ricambi, Fornitori, Ubicazioni | Anagrafiche |

## Scatole con QR e app per i tecnici

Ogni **scatola** in magazzino contiene un solo ricambio, sta in un'ubicazione e porta un'etichetta
con un QR (codice `SC-000001`, numerazione unica che non cambia). Dalla pagina **Scatole e QR**
si creano le scatole (anche tutte insieme per le giacenze esistenti) e si stampano le etichette
62 x 40 mm.

Il QR contiene l'indirizzo `<IndirizzoPubblico>/t/s/SC-000001`: il tecnico lo inquadra con la
fotocamera del telefono, tocca il link e si apre il prelievo di quel ricambio da quella ubicazione.
Sceglie linea (obbligatoria), macchina (facoltativa), motivo e quantita': il prelievo scala la
giacenza e crea l'intervento sulla linea, oppure aggiunge il pezzo a un intervento gia' in corso
sulla stessa linea. Da **I miei interventi** si rimettono in magazzino i pezzi non usati e si chiude
l'intervento con descrizione, ore e destino del pezzo smontato.

Configurazione in `appsettings.json`, sezione `Magazzino`:
- `IndirizzoPubblico`: indirizzo del server raggiungibile dai telefoni (es. `http://192.168.93.14:5080`).
  Va impostato prima di stampare le etichette, perche' finisce dentro il QR.
- `CostoOrario`: costo orario della manodopera per gli interventi.

## Accessi e ruoli

Ogni persona entra con il suo nome utente; il suo nome viene registrato su movimenti, ordini,
prelievi e interventi.

| Ruolo | Cosa puo' fare |
|---|---|
| Tecnico | Solo l'app dal telefono (`/t`): prelievi con QR, resi, chiusura interventi. Entra con un PIN di almeno 4 cifre. |
| Ufficio | Tutte le pagine dell'ufficio e l'app tecnici. Password di almeno 8 caratteri. |
| Responsabile | Come ufficio, e in piu' approva gli ordini sopra soglia. |
| Amministratore | Tutto, e gestisce utenti e ruoli dalla pagina **Utenti e ruoli**. |

- **Primo avvio:** se non esiste ancora nessun amministratore, la pagina di accesso chiede di crearlo.
  Farlo subito dopo l'installazione.
- Sul telefono l'accesso resta valido 30 giorni: il tecnico inserisce il PIN una volta sola.
  Se un QR viene inquadrato senza aver fatto l'accesso, dopo il PIN si apre direttamente quella scatola.
- Se l'amministratore disattiva un utente, gli cambia ruolo o password, le sessioni aperte
  (anche sui telefoni) si chiudono alla richiesta successiva.
- Ci deve sempre essere almeno un amministratore attivo.
- Le password sono salvate solo come hash (PBKDF2). Il cookie di accesso e' cifrato con le chiavi
  di ASP.NET Core Data Protection, salvate nel profilo dell'utente che esegue il servizio: se le
  chiavi vanno perse, tutti devono solo rientrare.
- Gli utenti del vecchio programma non vengono importati: si ricreano qui con i nuovi ruoli.

## Installazione su Windows

Il setup `Magazzino-ricambi-setup-x.y.z.exe` si scarica da GitHub: **Actions > Setup Windows >**
l'ultima esecuzione verde > **Artifacts** (oppure dalle Release, quando c'e' un tag `v*`).

1. Sul PC che fa da centrale (resta acceso in orario di lavoro) lanciare il setup come amministratore.
   Installa programma e database PostgreSQL, i servizi che partono all'accensione, la porta 5080
   nel firewall per la rete aziendale e l'icona **Magazzino ricambi** sul desktop.
2. Aprire l'icona e creare l'amministratore (prima pagina). Poi da **Utenti e ruoli** creare ufficio e tecnici.
3. Facoltativo: menu Start > **Magazzino ricambi > Importa dati da Gestione Manutenzione**, scegliere
   `manutenzione.db`. Prima fa una prova e mostra il resoconto, poi chiede conferma. Si fa una volta sola,
   su database vuoto (si puo' fare anche dopo aver creato gli utenti).
4. Telefoni e altri PC: aprire `http://<IP del PC>:5080` (l'indirizzo esatto e' in
   `C:\ProgramData\MagazzinoRicambi\impostazioni.json`, voce `IndirizzoPubblico`, che finisce anche nei QR).
   Conviene dare al PC un IP fisso, altrimenti i QR stampati smettono di funzionare se l'IP cambia.

Dati, impostazioni, password del database (`credenziali-database.txt`) e backup stanno in
`C:\ProgramData\MagazzinoRicambi`. Backup automatico ogni sera alle 21:00 (ultimi 30), o dal menu
Start con **Backup adesso**: copiare ogni tanto la cartella `backup` anche su un altro disco.
Per aggiornare si lancia il setup nuovo sopra quello vecchio; la disinstallazione toglie il programma
ma lascia i dati.

## Piano

1. Modello dati e progetto (questo scheletro)
2. Importazione da Gestione Manutenzione (`manutenzione.db`)
3. Gestione ordini per l'ufficio
4. App per i tecnici con QR sulle scatole
5. Accessi con utente e ruoli
6. Avvio sul server e importazione vera
