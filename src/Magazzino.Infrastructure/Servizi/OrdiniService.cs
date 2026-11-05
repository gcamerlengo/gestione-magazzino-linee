using Magazzino.Domain.Carico;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

public record RigaNuovoOrdine(int RicambioId, decimal Quantita, decimal? PrezzoUnitario = null);

/// <summary>Creazione e modifica degli ordini fornitore. Le righe si modificano solo in Bozza.</summary>
public class OrdiniService(MagazzinoDbContext db, Numeratore numeratore)
{
    /// <summary>Soglia sopra la quale l'ordine passa da "Da approvare" (0 = nessuna approvazione).</summary>
    public decimal SogliaApprovazione { get; set; }

    public async Task<OrdineFornitore> CreaAsync(int fornitoreId, IEnumerable<RigaNuovoOrdine> righe, DateTime? consegnaPrevista, string note = "")
    {
        var forn = await db.Fornitori.FindAsync(fornitoreId) ?? throw new MagazzinoException("Fornitore inesistente.");
        if (!forn.Attivo) throw new MagazzinoException($"Il fornitore {forn.RagioneSociale} non e' attivo.");
        var o = new OrdineFornitore
        {
            Numero = (await numeratore.ProssimoAsync("OF", DateTime.UtcNow)).Numero,
            FornitoreId = fornitoreId, DataConsegnaPrevista = Utc(consegnaPrevista), Note = note
        };
        foreach (var r in righe) await AggiungiRigaInternaAsync(o, r);
        db.OrdiniFornitore.Add(o);
        await db.SaveChangesAsync();
        return o;
    }

    /// <summary>Un ordine per fornitore preferito a partire dalla proposta di riordino. Le proposte senza fornitore restano fuori.</summary>
    public async Task<List<OrdineFornitore>> CreaDaProposteAsync(IEnumerable<PropostaRiordino> proposte)
    {
        var res = new List<OrdineFornitore>();
        foreach (var g in proposte.Where(p => p.FornitoreId is not null).GroupBy(p => p.FornitoreId!.Value))
            res.Add(await CreaAsync(g.Key, g.Select(p => new RigaNuovoOrdine(p.RicambioId, p.QuantitaProposta)), null, "Da proposta di riordino"));
        if (res.Count == 0) throw new MagazzinoException("Nessuna proposta con fornitore preferito: impostalo sui ricambi.");
        return res;
    }

    public async Task AggiungiRigaAsync(int ordineId, RigaNuovoOrdine riga)
    {
        var o = await InBozzaAsync(ordineId);
        await AggiungiRigaInternaAsync(o, riga);
        await db.SaveChangesAsync();
    }

    public async Task ModificaRigaAsync(int rigaId, decimal quantita, decimal prezzo)
    {
        var r = await db.RigheOrdineFornitore.FindAsync(rigaId) ?? throw new MagazzinoException("Riga inesistente.");
        await InBozzaAsync(r.OrdineFornitoreId);
        if (quantita <= 0) throw new MagazzinoException("La quantita' deve essere maggiore di zero.");
        if (prezzo < 0) throw new MagazzinoException("Il prezzo non puo' essere negativo.");
        r.Quantita = quantita;
        r.PrezzoUnitario = prezzo;
        await db.SaveChangesAsync();
    }

    public async Task EliminaRigaAsync(int rigaId)
    {
        var r = await db.RigheOrdineFornitore.FindAsync(rigaId) ?? throw new MagazzinoException("Riga inesistente.");
        await InBozzaAsync(r.OrdineFornitoreId);
        db.RigheOrdineFornitore.Remove(r);
        await db.SaveChangesAsync();
    }

    /// <summary>Dalla bozza: va in approvazione se supera la soglia, altrimenti e' inviato.</summary>
    public async Task<StatoOrdineFornitore> ConfermaBozzaAsync(int ordineId)
    {
        var o = await InBozzaAsync(ordineId);
        if (o.Righe.Count == 0) throw new MagazzinoException("L'ordine non ha righe.");
        o.Stato = SogliaApprovazione > 0 && o.Totale > SogliaApprovazione ? StatoOrdineFornitore.DaApprovare : StatoOrdineFornitore.Inviato;
        if (o.Stato == StatoOrdineFornitore.Inviato) o.DataOrdine = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return o.Stato;
    }

    public async Task ImpostaConsegnaPrevistaAsync(int ordineId, DateTime? data)
    {
        var o = await db.OrdiniFornitore.FindAsync(ordineId) ?? throw new MagazzinoException("Ordine inesistente.");
        o.DataConsegnaPrevista = Utc(data);
        await db.SaveChangesAsync();
    }

    public async Task<List<OrdineFornitore>> InRitardoAsync(DateTime oggiUtc)
    {
        var aperti = new[] { StatoOrdineFornitore.Inviato, StatoOrdineFornitore.Confermato, StatoOrdineFornitore.RicevutoInParte };
        return await db.OrdiniFornitore.Include(o => o.Fornitore)
            .Where(o => aperti.Contains(o.Stato) && o.DataConsegnaPrevista != null && o.DataConsegnaPrevista < oggiUtc.Date)
            .OrderBy(o => o.DataConsegnaPrevista).ToListAsync();
    }

    private async Task AggiungiRigaInternaAsync(OrdineFornitore o, RigaNuovoOrdine r)
    {
        if (r.Quantita <= 0) throw new MagazzinoException("La quantita' deve essere maggiore di zero.");
        var rc = await db.Ricambi.FindAsync(r.RicambioId) ?? throw new MagazzinoException("Ricambio inesistente.");
        var rf = await db.RicambiFornitori.FindAsync(r.RicambioId, o.FornitoreId);
        o.Righe.Add(new RigaOrdineFornitore
        {
            NumeroRiga = o.Righe.Count == 0 ? 1 : o.Righe.Max(x => x.NumeroRiga) + 1,
            RicambioId = rc.Id, CodiceFornitore = rf?.CodiceFornitore ?? rc.CodiceCostruttore, Descrizione = rc.Descrizione,
            UnitaMisura = rc.UnitaMisura, Quantita = r.Quantita, PrezzoUnitario = r.PrezzoUnitario ?? rf?.Prezzo ?? rc.CostoMedio
        });
    }

    private async Task<OrdineFornitore> InBozzaAsync(int ordineId)
    {
        var o = await db.OrdiniFornitore.Include(x => x.Righe).FirstOrDefaultAsync(x => x.Id == ordineId)
                ?? throw new MagazzinoException("Ordine inesistente.");
        if (o.Stato != StatoOrdineFornitore.Bozza) throw new MagazzinoException($"L'ordine {o.Numero} non e' piu' in bozza: le righe non si modificano.");
        return o;
    }

    /// <summary>Le date scelte nelle pagine arrivano senza fuso: PostgreSQL (timestamptz) le vuole in UTC.</summary>
    public static DateTime? Utc(DateTime? d) => d is null ? null : Utc(d.Value);
    public static DateTime Utc(DateTime d) => d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
}
