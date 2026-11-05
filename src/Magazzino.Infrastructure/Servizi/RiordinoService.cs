using Magazzino.Domain.Carico;
using Magazzino.Domain.Scarico;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

public record PropostaRiordino(int RicambioId, string Codice, string Descrizione, decimal Giacenza, decimal Impegnato,
    decimal InArrivo, decimal PuntoRiordino, decimal QuantitaProposta, int? FornitoreId, bool Critico)
{
    public decimal Disponibile => Giacenza - Impegnato;
}

public class RiordinoService(MagazzinoDbContext db, Numeratore numeratore)
{
    public async Task<List<PropostaRiordino>> ProponiAsync()
    {
        var ricambi = await db.Ricambi.Where(r => r.Attivo && r.PuntoRiordino > 0).ToListAsync();
        var giacenze = (await db.Giacenze.Select(g => new { g.RicambioId, g.Quantita }).ToListAsync())
            .GroupBy(g => g.RicambioId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantita));
        var impegnato = (await db.BuoniPrelievo.Where(b => b.Stato == StatoBuono.Aperto).SelectMany(b => b.Righe)
                .Select(r => new { r.RicambioId, r.Quantita }).ToListAsync())
            .GroupBy(r => r.RicambioId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantita));
        var statiInArrivo = new[] { StatoOrdineFornitore.Bozza, StatoOrdineFornitore.DaApprovare, StatoOrdineFornitore.Inviato, StatoOrdineFornitore.Confermato, StatoOrdineFornitore.RicevutoInParte };
        var inArrivo = (await db.RigheOrdineFornitore.Where(r => r.RicambioId != null && statiInArrivo.Contains(r.Ordine!.Stato))
                .Select(r => new { r.RicambioId, r.Quantita, r.QuantitaRicevuta }).ToListAsync())
            .GroupBy(r => r.RicambioId!.Value).ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.Quantita - x.QuantitaRicevuta)));
        var gia = (await db.RichiesteAcquisto.Where(r => r.Stato == StatoRichiesta.Aperta).SelectMany(r => r.Righe)
                .Select(r => new { r.RicambioId, r.Quantita }).ToListAsync())
            .GroupBy(r => r.RicambioId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantita));

        var res = new List<PropostaRiordino>();
        foreach (var r in ricambi)
        {
            var g = giacenze.GetValueOrDefault(r.Id);
            var imp = impegnato.GetValueOrDefault(r.Id);
            var arr = inArrivo.GetValueOrDefault(r.Id) + gia.GetValueOrDefault(r.Id);
            if (g - imp + arr >= r.PuntoRiordino) continue;
            var qta = r.QuantitaRiordino > 0 ? r.QuantitaRiordino : r.PuntoRiordino - (g - imp + arr);
            res.Add(new PropostaRiordino(r.Id, r.Codice, r.Descrizione, g, imp, arr, r.PuntoRiordino, qta, r.FornitorePreferitoId, r.Critico));
        }
        return res.OrderByDescending(p => p.Critico).ThenBy(p => p.Disponibile - p.PuntoRiordino).ToList();
    }

    public async Task<RichiestaAcquisto> CreaRichiestaAsync(IEnumerable<PropostaRiordino> proposte, string utente)
    {
        var rda = new RichiestaAcquisto
        {
            Numero = (await numeratore.ProssimoAsync("RDA", DateTime.UtcNow)).Numero,
            Richiedente = utente, DaRiordinoAutomatico = true,
            Righe = proposte.Select(p => new RigaRichiestaAcquisto { RicambioId = p.RicambioId, Quantita = p.QuantitaProposta, Motivo = "Sotto punto di riordino" }).ToList()
        };
        if (rda.Righe.Count == 0) throw new MagazzinoException("Nessun ricambio selezionato.");
        db.RichiesteAcquisto.Add(rda);
        await db.SaveChangesAsync();
        return rda;
    }
}
