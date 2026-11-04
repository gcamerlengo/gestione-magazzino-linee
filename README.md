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

## Piano

1. Modello dati e progetto (questo scheletro)
2. Importazione da Gestione Manutenzione (`manutenzione.db`)
3. Gestione ordini per l'ufficio
4. App per i tecnici e avvio
