using Magazzino.Domain.Anagrafiche;
using Magazzino.Domain.Carico;
using Magazzino.Infrastructure.Servizi;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Tests;

public class OrdiniTests
{
    [Fact]
    public async Task Da_proposta_a_ricevimento_completo()
    {
        using var t = new TestDb();
        t.Db.RicambiFornitori.Add(new RicambioFornitore { RicambioId = t.Cuscinetto.Id, FornitoreId = t.Fornitore.Id, CodiceFornitore = "SKF-6204", Prezzo = 4.2m });
        await t.Db.SaveChangesAsync();

        var proposte = await t.Riordino.ProponiAsync();
        var ordini = await t.Ordini.CreaDaProposteAsync(proposte);
        var o = Assert.Single(ordini);
        Assert.Equal(StatoOrdineFornitore.Bozza, o.Stato);
        Assert.StartsWith("OF-", o.Numero);
        var riga = Assert.Single(o.Righe);
        Assert.Equal(10, riga.Quantita);
        Assert.Equal(4.2m, riga.PrezzoUnitario);
        Assert.Equal("SKF-6204", riga.CodiceFornitore);

        // La bozza conta gia' come in arrivo: niente doppio ordine.
        Assert.Empty(await t.Riordino.ProponiAsync());

        Assert.Equal(StatoOrdineFornitore.Inviato, await t.Ordini.ConfermaBozzaAsync(o.Id));
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Ordini.AggiungiRigaAsync(o.Id, new RigaNuovoOrdine(t.Cuscinetto.Id, 1)));

        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT 9", new DateTime(2026, 11, 4), [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 10, 4.2m, riga.Id)], "u");
        Assert.Equal(StatoOrdineFornitore.Ricevuto, (await t.Db.OrdiniFornitore.FindAsync(o.Id))!.Stato);
        var ric = await t.Db.Ricevimenti.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, ric.DataDdt.Kind);
    }

    [Fact]
    public async Task Sopra_soglia_serve_approvazione()
    {
        using var t = new TestDb();
        t.Ordini.SogliaApprovazione = 100;
        var o = await t.Ordini.CreaAsync(t.Fornitore.Id, [new RigaNuovoOrdine(t.Cuscinetto.Id, 50, 5m)], null);
        Assert.Equal(StatoOrdineFornitore.DaApprovare, await t.Ordini.ConfermaBozzaAsync(o.Id));
        await t.Carico.CambiaStatoOrdineAsync(o.Id, StatoOrdineFornitore.Inviato, "responsabile");
        var dopo = await t.Db.OrdiniFornitore.AsNoTracking().SingleAsync(x => x.Id == o.Id);
        Assert.Equal("responsabile", dopo.ApprovatoDa);
    }

    [Fact]
    public async Task Bozza_senza_righe_non_si_conferma_e_righe_si_modificano()
    {
        using var t = new TestDb();
        var o = await t.Ordini.CreaAsync(t.Fornitore.Id, [], null);
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Ordini.ConfermaBozzaAsync(o.Id));
        await t.Ordini.AggiungiRigaAsync(o.Id, new RigaNuovoOrdine(t.Cuscinetto.Id, 3, 2m));
        await t.Ordini.AggiungiRigaAsync(o.Id, new RigaNuovoOrdine(t.Cuscinetto.Id, 1, 2m));
        var righe = await t.Db.RigheOrdineFornitore.Where(r => r.OrdineFornitoreId == o.Id).OrderBy(r => r.NumeroRiga).ToListAsync();
        Assert.Equal([1, 2], righe.Select(r => r.NumeroRiga));
        await t.Ordini.ModificaRigaAsync(righe[0].Id, 5, 2.5m);
        await t.Ordini.EliminaRigaAsync(righe[1].Id);
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Ordini.ModificaRigaAsync(righe[0].Id, 0, 1));
        t.Db.ChangeTracker.Clear();
        var finale = await t.Db.OrdiniFornitore.Include(x => x.Righe).SingleAsync(x => x.Id == o.Id);
        Assert.Equal(12.5m, finale.Totale);
    }

    [Fact]
    public async Task Proposte_senza_fornitore_non_creano_ordini()
    {
        using var t = new TestDb();
        t.Cuscinetto.FornitorePreferitoId = null;
        t.Cuscinetto.FornitorePreferito = null;
        await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<MagazzinoException>(async () => await t.Ordini.CreaDaProposteAsync(await t.Riordino.ProponiAsync()));
    }

    [Fact]
    public async Task Fornitore_non_attivo_rifiutato()
    {
        using var t = new TestDb();
        t.Fornitore.Attivo = false;
        await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Ordini.CreaAsync(t.Fornitore.Id, [], null));
    }
}
