using Magazzino.Domain.Giacenze;
using Magazzino.Domain.Scarico;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

public record RigaDaPrelevare(int RicambioId, int UbicazioneId, decimal Quantita);

public class ScaricoService(MagazzinoDbContext db, GiacenzeService giacenze, Numeratore numeratore)
{
    /// <summary>
    /// Prelievo per una linea: scarica i pezzi e, nella stessa transazione, crea l'intervento sulla linea
    /// (o aggiunge i ricambi a un intervento in corso indicato).
    /// </summary>
    public async Task<BuonoPrelievo> PrelevaPerLineaAsync(int lineaId, int? macchinaId, MotivoIntervento motivo,
        IReadOnlyList<RigaDaPrelevare> righe, string tecnico, int? interventoEsistenteId = null)
    {
        if (righe.Count == 0) throw new MagazzinoException("Il buono non ha righe.");
        if (!await db.Linee.AnyAsync(l => l.Id == lineaId)) throw new MagazzinoException("Linea inesistente.");
        if (macchinaId is int mid && !await db.Macchine.AnyAsync(m => m.Id == mid && m.LineaId == lineaId))
            throw new MagazzinoException("La macchina non appartiene alla linea scelta.");

        await using var tx = await db.Database.BeginTransactionAsync();
        Intervento intervento;
        if (interventoEsistenteId is int iid)
        {
            intervento = await db.Interventi.FindAsync(iid) ?? throw new MagazzinoException("Intervento inesistente.");
            if (intervento.LineaId != lineaId) throw new MagazzinoException("L'intervento e' di un'altra linea.");
            if (intervento.Stato != StatoIntervento.InCorso) throw new MagazzinoException("L'intervento non e' in corso.");
        }
        else
        {
            intervento = new Intervento
            {
                Codice = (await numeratore.ProssimoAsync("INT", DateTime.UtcNow)).Numero,
                LineaId = lineaId, MacchinaId = macchinaId, Motivo = motivo, Tecnico = tecnico
            };
            db.Interventi.Add(intervento);
        }
        var buono = new BuonoPrelievo
        {
            Numero = (await numeratore.ProssimoAsync("BP", DateTime.UtcNow)).Numero,
            LineaId = lineaId, MacchinaId = macchinaId, Motivo = motivo, Tecnico = tecnico,
            Stato = StatoBuono.Prelevato, Intervento = intervento
        };
        db.BuoniPrelievo.Add(buono);
        await db.SaveChangesAsync();

        foreach (var x in righe)
        {
            var ricambio = await db.Ricambi.FindAsync(x.RicambioId) ?? throw new MagazzinoException($"Ricambio {x.RicambioId} inesistente.");
            var mov = await giacenze.UscitaAsync(TipoMovimento.ScaricoIntervento, ricambio, x.UbicazioneId, x.Quantita, tecnico,
                nameof(BuonoPrelievo), buono.Id, lineaId, intervento.Id);
            buono.Righe.Add(new RigaBuonoPrelievo { RicambioId = x.RicambioId, UbicazioneId = x.UbicazioneId, Quantita = x.Quantita });
            await db.SaveChangesAsync();
            intervento.RicambiUsati.Add(new RicambioUsato
            {
                RicambioId = x.RicambioId, Quantita = x.Quantita, CostoUnitario = ricambio.CostoMedio, MovimentoId = mov.Id
            });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return buono;
    }

    /// <summary>Scarico senza linea (consumo generico): motivo obbligatorio, nessun intervento.</summary>
    public async Task<BuonoPrelievo> ScaricoGenericoAsync(string motivo, IReadOnlyList<RigaDaPrelevare> righe, string utente)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new MagazzinoException("Per uno scarico senza linea il motivo e' obbligatorio.");
        if (righe.Count == 0) throw new MagazzinoException("Il buono non ha righe.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var buono = new BuonoPrelievo
        {
            Numero = (await numeratore.ProssimoAsync("BP", DateTime.UtcNow)).Numero,
            Motivo = MotivoIntervento.Altro, MotivoScaricoGenerico = motivo, Tecnico = utente, Stato = StatoBuono.Completato
        };
        db.BuoniPrelievo.Add(buono);
        await db.SaveChangesAsync();
        foreach (var x in righe)
        {
            var ricambio = await db.Ricambi.FindAsync(x.RicambioId) ?? throw new MagazzinoException($"Ricambio {x.RicambioId} inesistente.");
            await giacenze.UscitaAsync(TipoMovimento.ScaricoGenerico, ricambio, x.UbicazioneId, x.Quantita, utente, nameof(BuonoPrelievo), buono.Id);
            buono.Righe.Add(new RigaBuonoPrelievo { RicambioId = x.RicambioId, UbicazioneId = x.UbicazioneId, Quantita = x.Quantita });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return buono;
    }

    /// <summary>Pezzo prelevato e non montato: torna a magazzino e viene tolto dall'intervento.</summary>
    public async Task RendiAsync(int rigaBuonoId, decimal quantita, string utente)
    {
        var riga = await db.Set<RigaBuonoPrelievo>().FindAsync(rigaBuonoId) ?? throw new MagazzinoException("Riga del buono inesistente.");
        if (quantita > riga.Quantita - riga.QuantitaResa) throw new MagazzinoException("Si rende piu' di quanto prelevato.");
        var buono = await db.BuoniPrelievo.Include(b => b.Righe).Include(b => b.Intervento).ThenInclude(i => i!.RicambiUsati)
            .FirstAsync(b => b.Id == riga.BuonoPrelievoId);
        var ricambio = (await db.Ricambi.FindAsync(riga.RicambioId))!;

        await using var tx = await db.Database.BeginTransactionAsync();
        await giacenze.EntrataAsync(TipoMovimento.Reso, ricambio, riga.UbicazioneId, quantita, ricambio.CostoMedio, utente,
            nameof(BuonoPrelievo), buono.Id, aggiornaCostoMedio: false);
        riga.QuantitaResa += quantita;

        if (buono.Intervento is { } i)
        {
            var daTogliere = quantita;
            foreach (var u in i.RicambiUsati.Where(u => u.RicambioId == riga.RicambioId).OrderByDescending(u => u.Id))
            {
                var t = Math.Min(u.Quantita, daTogliere);
                u.Quantita -= t;
                daTogliere -= t;
                if (daTogliere == 0) break;
            }
            i.RicambiUsati.RemoveAll(u => u.Quantita == 0);
        }
        if (buono.Righe.All(r => r.QuantitaResa == r.Quantita))
        {
            buono.Stato = StatoBuono.Annullato;
            if (buono.Intervento is { RicambiUsati.Count: 0, Stato: StatoIntervento.InCorso } vuoto)
                vuoto.Stato = StatoIntervento.Annullato;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task CompletaInterventoAsync(int interventoId, decimal ore, string descrizione, DestinoPezzoSmontato pezzo,
        decimal costoOrario, decimal costoEsterni = 0)
    {
        var i = await db.Interventi.FindAsync(interventoId) ?? throw new MagazzinoException("Intervento inesistente.");
        if (i.Stato != StatoIntervento.InCorso) throw new MagazzinoException("L'intervento non e' in corso.");
        if (string.IsNullOrWhiteSpace(descrizione)) throw new MagazzinoException("Descrivi il lavoro fatto.");
        i.OreLavoro = ore;
        i.Descrizione = descrizione;
        i.PezzoSmontato = pezzo;
        i.CostoManodopera = Math.Round(ore * costoOrario, 2);
        i.CostoEsterni = costoEsterni;
        i.Fine = DateTime.UtcNow;
        i.Stato = StatoIntervento.Completato;
        foreach (var b in await db.BuoniPrelievo.Where(b => b.InterventoId == i.Id && b.Stato == StatoBuono.Prelevato).ToListAsync())
            b.Stato = StatoBuono.Completato;
        await db.SaveChangesAsync();
    }
}
