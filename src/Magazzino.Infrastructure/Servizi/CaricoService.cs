using Magazzino.Domain.Carico;
using Magazzino.Domain.Giacenze;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

public record RigaDaRicevere(int RicambioId, int UbicazioneId, decimal Quantita, decimal PrezzoUnitario, int? RigaOrdineFornitoreId = null);

public class CaricoService(MagazzinoDbContext db, GiacenzeService giacenze, Numeratore numeratore)
{
    /// <summary>Registra un DDT: carica la merce, aggiorna le righe d'ordine e lo stato degli ordini, tutto in una transazione.</summary>
    public async Task<Ricevimento> RegistraRicevimentoAsync(int fornitoreId, string numeroDdt, DateTime dataDdt,
        IReadOnlyList<RigaDaRicevere> righe, string utente, string note = "")
    {
        if (righe.Count == 0) throw new MagazzinoException("Il ricevimento non ha righe.");
        if (string.IsNullOrWhiteSpace(numeroDdt)) throw new MagazzinoException("Numero DDT obbligatorio.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var r = new Ricevimento
        {
            Numero = (await numeratore.ProssimoAsync("RIC", DateTime.UtcNow)).Numero,
            FornitoreId = fornitoreId, NumeroDdt = numeroDdt, DataDdt = dataDdt, RicevutoDa = utente, Note = note
        };
        db.Ricevimenti.Add(r);
        await db.SaveChangesAsync();

        var ordiniToccati = new HashSet<int>();
        foreach (var x in righe)
        {
            var ricambio = await db.Ricambi.FindAsync(x.RicambioId) ?? throw new MagazzinoException($"Ricambio {x.RicambioId} inesistente.");
            if (x.RigaOrdineFornitoreId is int idRiga)
            {
                var ro = await db.RigheOrdineFornitore.Include(o => o.Ordine).FirstOrDefaultAsync(o => o.Id == idRiga)
                         ?? throw new MagazzinoException("Riga d'ordine inesistente.");
                if (ro.Ordine!.FornitoreId != fornitoreId) throw new MagazzinoException($"La riga d'ordine non e' del fornitore del DDT.");
                if (ro.Ordine.Stato is not (StatoOrdineFornitore.Inviato or StatoOrdineFornitore.Confermato or StatoOrdineFornitore.RicevutoInParte))
                    throw new MagazzinoException($"L'ordine {ro.Ordine.Numero} e' in stato {ro.Ordine.Stato}: non si puo' ricevere.");
                if (ro.RicambioId != x.RicambioId) throw new MagazzinoException("Il ricambio non corrisponde alla riga d'ordine.");
                ro.QuantitaRicevuta += x.Quantita;
                ordiniToccati.Add(ro.OrdineFornitoreId);
            }
            r.Righe.Add(new RigaRicevimento
            {
                RicambioId = x.RicambioId, UbicazioneId = x.UbicazioneId, Quantita = x.Quantita,
                PrezzoUnitario = x.PrezzoUnitario, RigaOrdineFornitoreId = x.RigaOrdineFornitoreId
            });
            await giacenze.EntrataAsync(TipoMovimento.CaricoFornitore, ricambio, x.UbicazioneId, x.Quantita, x.PrezzoUnitario,
                utente, nameof(Ricevimento), r.Id, aggiornaCostoMedio: true);
        }

        foreach (var id in ordiniToccati)
        {
            var o = await db.OrdiniFornitore.Include(o => o.Righe).FirstAsync(o => o.Id == id);
            o.Stato = o.Righe.All(l => l.Residuo == 0) ? StatoOrdineFornitore.Ricevuto : StatoOrdineFornitore.RicevutoInParte;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return r;
    }

    public async Task CambiaStatoOrdineAsync(int ordineId, StatoOrdineFornitore nuovo, string utente)
    {
        var o = await db.OrdiniFornitore.Include(x => x.Righe).FirstOrDefaultAsync(x => x.Id == ordineId)
                ?? throw new MagazzinoException("Ordine inesistente.");
        if (!TransizioneAmmessa(o.Stato, nuovo))
            throw new MagazzinoException($"Passaggio da {o.Stato} a {nuovo} non ammesso.");
        if (nuovo == StatoOrdineFornitore.Annullato && o.Righe.Any(r => r.QuantitaRicevuta > 0))
            throw new MagazzinoException("Ordine gia' ricevuto in parte: chiudilo invece di annullarlo.");
        if (o.Stato == StatoOrdineFornitore.DaApprovare && nuovo == StatoOrdineFornitore.Inviato)
        {
            o.ApprovatoDa = utente;
            o.ApprovatoIl = DateTime.UtcNow;
        }
        o.Stato = nuovo;
        await db.SaveChangesAsync();
    }

    public static bool TransizioneAmmessa(StatoOrdineFornitore da, StatoOrdineFornitore a) => (da, a) switch
    {
        (StatoOrdineFornitore.Bozza, StatoOrdineFornitore.DaApprovare or StatoOrdineFornitore.Inviato or StatoOrdineFornitore.Annullato) => true,
        (StatoOrdineFornitore.DaApprovare, StatoOrdineFornitore.Inviato or StatoOrdineFornitore.Bozza or StatoOrdineFornitore.Annullato) => true,
        (StatoOrdineFornitore.Inviato, StatoOrdineFornitore.Confermato or StatoOrdineFornitore.Annullato) => true,
        (StatoOrdineFornitore.Confermato, StatoOrdineFornitore.Annullato) => true,
        (StatoOrdineFornitore.RicevutoInParte or StatoOrdineFornitore.Ricevuto, StatoOrdineFornitore.Chiuso) => true,
        _ => false
    };
}
