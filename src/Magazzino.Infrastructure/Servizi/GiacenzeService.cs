using Magazzino.Domain.Anagrafiche;
using Magazzino.Domain.Giacenze;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

/// <summary>
/// Unico punto che crea movimenti e aggiorna le giacenze. Non salva: il chiamante salva
/// documento + movimenti + giacenze in un'unica SaveChanges (una transazione).
/// </summary>
public class GiacenzeService(MagazzinoDbContext db, Numeratore numeratore)
{
    public async Task<Movimento> EntrataAsync(TipoMovimento tipo, Ricambio ricambio, int ubicazioneId, decimal quantita,
        decimal costoUnitario, string utente, string tipoDocumento, int? documentoId, bool aggiornaCostoMedio)
    {
        ControllaQuantita(quantita);
        var g = await GiacenzaAsync(ricambio.Id, ubicazioneId);
        if (aggiornaCostoMedio)
        {
            var totale = await TotaleRicambioAsync(ricambio.Id);
            var nuovaQta = totale + quantita;
            ricambio.CostoMedio = nuovaQta <= 0 ? costoUnitario
                : Math.Round((Math.Max(totale, 0) * ricambio.CostoMedio + quantita * costoUnitario) / nuovaQta, 4);
        }
        g.Quantita += quantita;
        g.UltimoMovimento = DateTime.UtcNow;
        return await NuovoMovimentoAsync(tipo, ricambio.Id, null, ubicazioneId, quantita, costoUnitario, utente, tipoDocumento, documentoId);
    }

    public async Task<Movimento> UscitaAsync(TipoMovimento tipo, Ricambio ricambio, int ubicazioneId, decimal quantita,
        string utente, string tipoDocumento, int? documentoId, int? lineaId = null, int? interventoId = null)
    {
        ControllaQuantita(quantita);
        var g = await GiacenzaAsync(ricambio.Id, ubicazioneId);
        if (g.Quantita < quantita)
            throw new MagazzinoException(
                $"Giacenza insufficiente per {ricambio.Codice}: disponibili {g.Quantita:0.##} {ricambio.UnitaMisura}, richiesti {quantita:0.##}.");
        g.Quantita -= quantita;
        g.UltimoMovimento = DateTime.UtcNow;
        var m = await NuovoMovimentoAsync(tipo, ricambio.Id, ubicazioneId, null, quantita, ricambio.CostoMedio, utente, tipoDocumento, documentoId);
        m.LineaId = lineaId;
        m.InterventoId = interventoId;
        return m;
    }

    public async Task<Movimento> TrasferisciAsync(Ricambio ricambio, int daId, int aId, decimal quantita, string utente)
    {
        if (daId == aId) throw new MagazzinoException("Ubicazione di partenza e di arrivo coincidono.");
        ControllaQuantita(quantita);
        var da = await GiacenzaAsync(ricambio.Id, daId);
        if (da.Quantita < quantita) throw new MagazzinoException($"Giacenza insufficiente per {ricambio.Codice} nell'ubicazione di partenza.");
        var a = await GiacenzaAsync(ricambio.Id, aId);
        da.Quantita -= quantita;
        a.Quantita += quantita;
        da.UltimoMovimento = a.UltimoMovimento = DateTime.UtcNow;
        return await NuovoMovimentoAsync(TipoMovimento.Trasferimento, ricambio.Id, daId, aId, quantita, ricambio.CostoMedio, utente, "", null);
    }

    /// <summary>Annulla un movimento con uno opposto; l'originale resta e viene marcato come stornato.</summary>
    public async Task<Movimento> StornaAsync(int movimentoId, string utente, string motivo)
    {
        var orig = await db.Movimenti.FindAsync(movimentoId) ?? throw new MagazzinoException("Movimento non trovato.");
        if (orig.Stornato) throw new MagazzinoException($"Il movimento {orig.Numero} e' gia' stornato.");
        if (orig.Tipo == TipoMovimento.Storno) throw new MagazzinoException("Non si storna uno storno.");
        if (orig.UbicazioneAId is int a)
        {
            var g = await GiacenzaAsync(orig.RicambioId, a);
            if (g.Quantita < orig.Quantita) throw new MagazzinoException("Lo storno renderebbe negativa la giacenza.");
            g.Quantita -= orig.Quantita;
        }
        if (orig.UbicazioneDaId is int d) (await GiacenzaAsync(orig.RicambioId, d)).Quantita += orig.Quantita;
        orig.Stornato = true;
        var m = await NuovoMovimentoAsync(TipoMovimento.Storno, orig.RicambioId, orig.UbicazioneAId, orig.UbicazioneDaId,
            orig.Quantita, orig.CostoUnitario, utente, orig.TipoDocumento, orig.DocumentoId);
        m.StornaMovimentoId = orig.Id;
        m.Note = motivo;
        return m;
    }

    public async Task<decimal> TotaleRicambioAsync(int ricambioId)
    {
        var salvate = await db.Giacenze.Where(g => g.RicambioId == ricambioId).Select(g => new { g.UbicazioneId, g.Quantita }).ToListAsync();
        var locali = db.Giacenze.Local.Where(g => g.RicambioId == ricambioId).ToList();
        return locali.Sum(g => g.Quantita) + salvate.Where(s => locali.All(l => l.UbicazioneId != s.UbicazioneId)).Sum(s => s.Quantita);
    }

    private async Task<Giacenza> GiacenzaAsync(int ricambioId, int ubicazioneId)
    {
        var g = db.Giacenze.Local.FirstOrDefault(x => x.RicambioId == ricambioId && x.UbicazioneId == ubicazioneId)
                ?? await db.Giacenze.FirstOrDefaultAsync(x => x.RicambioId == ricambioId && x.UbicazioneId == ubicazioneId);
        if (g is null)
        {
            if (!await db.Ubicazioni.AnyAsync(u => u.Id == ubicazioneId && u.Attiva))
                throw new MagazzinoException("Ubicazione inesistente o non attiva.");
            g = new Giacenza { RicambioId = ricambioId, UbicazioneId = ubicazioneId };
            db.Giacenze.Add(g);
        }
        return g;
    }

    private async Task<Movimento> NuovoMovimentoAsync(TipoMovimento tipo, int ricambioId, int? da, int? a, decimal qta,
        decimal costo, string utente, string tipoDoc, int? docId)
    {
        var now = DateTime.UtcNow;
        var (numero, anno, prog) = await numeratore.ProssimoAsync("MOV", now);
        var m = new Movimento
        {
            Numero = numero, Anno = anno, Progressivo = prog, Data = now, Utente = utente, Tipo = tipo,
            RicambioId = ricambioId, UbicazioneDaId = da, UbicazioneAId = a, Quantita = qta, CostoUnitario = costo,
            TipoDocumento = tipoDoc, DocumentoId = docId
        };
        db.Movimenti.Add(m);
        return m;
    }

    private static void ControllaQuantita(decimal q)
    {
        if (q <= 0) throw new MagazzinoException("La quantita' deve essere maggiore di zero.");
    }
}
