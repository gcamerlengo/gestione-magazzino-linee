using Magazzino.Domain.Carico;
using Magazzino.Domain.Giacenze;
using Magazzino.Domain.Scarico;
using Magazzino.Infrastructure.Servizi;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Tests;

public class MagazzinoTests
{
    private static async Task<OrdineFornitore> OrdineInviatoAsync(TestDb t, decimal qta, decimal prezzo)
    {
        var o = new OrdineFornitore
        {
            Numero = "OF-1", FornitoreId = t.Fornitore.Id, Stato = StatoOrdineFornitore.Inviato,
            Righe = [new RigaOrdineFornitore { NumeroRiga = 1, RicambioId = t.Cuscinetto.Id, Quantita = qta, PrezzoUnitario = prezzo }]
        };
        t.Db.OrdiniFornitore.Add(o);
        await t.Db.SaveChangesAsync();
        return o;
    }

    private static async Task<decimal> GiacenzaAsync(TestDb t, int ubicazioneId) =>
        (await t.Db.Giacenze.AsNoTracking().FirstOrDefaultAsync(g => g.RicambioId == t.Cuscinetto.Id && g.UbicazioneId == ubicazioneId))?.Quantita ?? 0;

    [Fact]
    public async Task Ricevimento_parziale_carica_e_aggiorna_stato_ordine()
    {
        using var t = new TestDb();
        var o = await OrdineInviatoAsync(t, 10, 5m);
        var riga = o.Righe[0];

        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT 1", DateTime.Today,
            [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 4, 5m, riga.Id)], "ufficio");
        Assert.Equal(4, await GiacenzaAsync(t, t.U1.Id));
        Assert.Equal(StatoOrdineFornitore.RicevutoInParte, (await t.Db.OrdiniFornitore.FindAsync(o.Id))!.Stato);

        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT 2", DateTime.Today,
            [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 6, 5m, riga.Id)], "ufficio");
        Assert.Equal(10, await GiacenzaAsync(t, t.U1.Id));
        Assert.Equal(StatoOrdineFornitore.Ricevuto, (await t.Db.OrdiniFornitore.FindAsync(o.Id))!.Stato);
        Assert.Equal(2, await t.Db.Movimenti.CountAsync(m => m.Tipo == TipoMovimento.CaricoFornitore));
    }

    [Fact]
    public async Task Costo_medio_ponderato()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT 1", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 10, 4m)], "u");
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT 2", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U2.Id, 10, 6m)], "u");
        Assert.Equal(5m, (await t.Db.Ricambi.FindAsync(t.Cuscinetto.Id))!.CostoMedio);
    }

    [Fact]
    public async Task Ordine_in_bozza_non_si_riceve()
    {
        using var t = new TestDb();
        var o = await OrdineInviatoAsync(t, 10, 5m);
        o.Stato = StatoOrdineFornitore.Bozza;
        await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today,
            [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 1, 5m, o.Righe[0].Id)], "u"));
        Assert.Equal(0, await GiacenzaAsync(t, t.U1.Id));
    }

    [Fact]
    public async Task Prelievo_per_linea_crea_intervento_e_scala_giacenza()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 5, 8m)], "u");

        var buono = await t.Scarico.PrelevaPerLineaAsync(t.Linea.Id, t.Macchina.Id, MotivoIntervento.Guasto,
            [new RigaDaPrelevare(t.Cuscinetto.Id, t.U1.Id, 2)], "tecnico");

        Assert.Equal(3, await GiacenzaAsync(t, t.U1.Id));
        var i = await t.Db.Interventi.Include(x => x.RicambiUsati).SingleAsync();
        Assert.Equal(t.Linea.Id, i.LineaId);
        Assert.Equal(buono.InterventoId, i.Id);
        Assert.Equal(16m, i.CostoRicambi);
        var mov = await t.Db.Movimenti.SingleAsync(m => m.Tipo == TipoMovimento.ScaricoIntervento);
        Assert.Equal(t.Linea.Id, mov.LineaId);
        Assert.Equal(i.Id, mov.InterventoId);
    }

    [Fact]
    public async Task Prelievo_oltre_giacenza_non_lascia_tracce()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 1, 8m)], "u");
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Scarico.PrelevaPerLineaAsync(t.Linea.Id, null, MotivoIntervento.Guasto,
            [new RigaDaPrelevare(t.Cuscinetto.Id, t.U1.Id, 2)], "tecnico"));
        t.Db.ChangeTracker.Clear();
        Assert.Equal(1, await GiacenzaAsync(t, t.U1.Id));
        Assert.Equal(0, await t.Db.Interventi.CountAsync());
        Assert.Equal(0, await t.Db.BuoniPrelievo.CountAsync());
    }

    [Fact]
    public async Task Macchina_di_altra_linea_rifiutata()
    {
        using var t = new TestDb();
        var altra = new Domain.Anagrafiche.Linea { Codice = "L2", Nome = "LINEA 2" };
        t.Db.Linee.Add(altra);
        await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Scarico.PrelevaPerLineaAsync(altra.Id, t.Macchina.Id, MotivoIntervento.Guasto,
            [new RigaDaPrelevare(t.Cuscinetto.Id, t.U1.Id, 1)], "tecnico"));
    }

    [Fact]
    public async Task Reso_totale_annulla_buono_e_intervento()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 5, 8m)], "u");
        var buono = await t.Scarico.PrelevaPerLineaAsync(t.Linea.Id, null, MotivoIntervento.Guasto, [new RigaDaPrelevare(t.Cuscinetto.Id, t.U1.Id, 2)], "tec");

        await t.Scarico.RendiAsync(buono.Righe[0].Id, 2, "tec");

        Assert.Equal(5, await GiacenzaAsync(t, t.U1.Id));
        Assert.Equal(StatoBuono.Annullato, (await t.Db.BuoniPrelievo.FindAsync(buono.Id))!.Stato);
        Assert.Equal(StatoIntervento.Annullato, (await t.Db.Interventi.FindAsync(buono.InterventoId))!.Stato);
    }

    [Fact]
    public async Task Storno_ripristina_giacenza()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 5, 8m)], "u");
        var mov = await t.Giacenze.TrasferisciAsync(t.Cuscinetto, t.U1.Id, t.U2.Id, 3, "u");
        await t.Db.SaveChangesAsync();
        await t.Giacenze.StornaAsync(mov.Id, "u", "errore");
        await t.Db.SaveChangesAsync();
        Assert.Equal(5, await GiacenzaAsync(t, t.U1.Id));
        Assert.Equal(0, await GiacenzaAsync(t, t.U2.Id));
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Giacenze.StornaAsync(mov.Id, "u", "di nuovo"));
    }

    [Fact]
    public async Task Riordino_considera_merce_in_arrivo()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 2, 8m)], "u");
        var proposte = await t.Riordino.ProponiAsync();
        Assert.Single(proposte);
        Assert.Equal(10, proposte[0].QuantitaProposta);

        await OrdineInviatoAsync(t, 10, 8m);
        Assert.Empty(await t.Riordino.ProponiAsync());
    }

    [Theory]
    [InlineData(StatoOrdineFornitore.Bozza, StatoOrdineFornitore.Inviato, true)]
    [InlineData(StatoOrdineFornitore.Bozza, StatoOrdineFornitore.Ricevuto, false)]
    [InlineData(StatoOrdineFornitore.Ricevuto, StatoOrdineFornitore.Chiuso, true)]
    [InlineData(StatoOrdineFornitore.Chiuso, StatoOrdineFornitore.Bozza, false)]
    public void Transizioni_stato_ordine(StatoOrdineFornitore da, StatoOrdineFornitore a, bool ammessa) =>
        Assert.Equal(ammessa, CaricoService.TransizioneAmmessa(da, a));
}
