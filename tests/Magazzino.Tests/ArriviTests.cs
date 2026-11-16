using Magazzino.Domain.Anagrafiche;
using Magazzino.Domain.Carico;
using Magazzino.Infrastructure.Servizi;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Tests;

public class ArriviTests
{
    private static ArrivoDaRisolvo Arrivo(string id, string materiale, string qta, string fornitore = "SKF") =>
        new(id, $"MAT-{id}", materiale, qta, "Linea 3", fornitore, "ORD-1", new DateTime(2026, 11, 6));

    [Theory]
    [InlineData("2 pezzi", 2.0)]
    [InlineData("1,5 kg", 1.5)]
    [InlineData("n. 3", 3.0)]
    [InlineData("qualche metro", null)]
    public void Quantita_dal_testo(string testo, double? attesa) =>
        Assert.Equal(attesa is null ? null : (decimal)attesa, ArriviService.LeggiQuantita(testo));

    [Fact]
    public async Task Codice_nel_testo_carica_da_solo()
    {
        using var t = new TestDb();
        t.Cuscinetto.UbicazionePredefinitaId = t.U1.Id;
        await t.Db.SaveChangesAsync();

        var (nuovi, caricati) = await t.Arrivi.RegistraAsync([Arrivo("a1", "Cuscinetto SKF 6204 2RS per etichettatrice", "4 pezzi")], caricaSeCerto: true);
        Assert.Equal((1, 1), (nuovi, caricati));
        var a = await t.Db.ArriviEsterni.SingleAsync();
        Assert.Equal(StatoArrivoEsterno.Caricato, a.Stato);
        Assert.True(a.CaricatoInAutomatico);
        Assert.Equal(4, await t.Giacenze.TotaleRicambioAsync(t.Cuscinetto.Id));
        Assert.Equal(t.Fornitore.Id, (await t.Db.Ricevimenti.SingleAsync()).FornitoreId); // "SKF" riconosciuto

        // Lo stesso arrivo letto di nuovo non viene caricato due volte.
        Assert.Equal((0, 0), await t.Arrivi.RegistraAsync([Arrivo("a1", "Cuscinetto SKF 6204 2RS", "4 pezzi")], true));
        Assert.Equal(4, await t.Giacenze.TotaleRicambioAsync(t.Cuscinetto.Id));
    }

    [Fact]
    public async Task Senza_codice_resta_da_confermare_e_poi_si_ricorda()
    {
        using var t = new TestDb();
        await t.Arrivi.RegistraAsync([Arrivo("b1", "cuscinetto per riempitrice", "2 pz", fornitore: "")], caricaSeCerto: true);
        var a = await t.Db.ArriviEsterni.SingleAsync();
        Assert.Equal(StatoArrivoEsterno.DaConfermare, a.Stato);
        Assert.False(a.Certo);
        Assert.Equal(0, await t.Giacenze.TotaleRicambioAsync(t.Cuscinetto.Id));

        await t.Arrivi.ConfermaAsync(a.Id, t.Cuscinetto.Id, 2, t.U2.Id, "anna");
        Assert.Equal(2, await t.Giacenze.TotaleRicambioAsync(t.Cuscinetto.Id));
        Assert.Equal(t.Fornitore.Id, (await t.Db.Ricevimenti.SingleAsync()).FornitoreId); // fornitore preferito del ricambio
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Arrivi.ConfermaAsync(a.Id, t.Cuscinetto.Id, 2, t.U2.Id, "anna"));

        // La volta dopo lo stesso testo e' riconosciuto con certezza.
        var p = await t.Arrivi.ProponiAsync("Cuscinetto  per RIEMPITRICE");
        Assert.True(p.Certo);
        Assert.Equal(t.Cuscinetto.Id, p.Ricambio!.Id);
    }

    [Fact]
    public async Task Codici_ambigui_e_scarto()
    {
        using var t = new TestDb();
        var altro = new Ricambio { Codice = "6204-ZZ", Descrizione = "Cuscinetto 6204 schermato" };
        var lungo = new Ricambio { Codice = "6204-2RS-C3", Descrizione = "Cuscinetto 6204 gioco C3" };
        t.Db.AddRange(altro, lungo);
        await t.Db.SaveChangesAsync();

        // 6204-2RS e 6204-2RS-C3 sono entrambi nel testo: vince il codice piu' lungo.
        var p = await t.Arrivi.ProponiAsync("cuscinetto 6204-2RS-C3");
        Assert.True(p.Certo);
        Assert.Equal(lungo.Id, p.Ricambio!.Id);

        await t.Arrivi.RegistraAsync([Arrivo("c1", "Olio per compressore", "1 tanica")], caricaSeCerto: true);
        var a = await t.Db.ArriviEsterni.SingleAsync();
        Assert.Null(a.RicambioId);
        await t.Arrivi.ScartaAsync(a.Id, "materiale di consumo", "anna");
        Assert.Equal(StatoArrivoEsterno.Scartato, (await t.Db.ArriviEsterni.SingleAsync()).Stato);
    }

    [Fact]
    public async Task Fornitore_sconosciuto_viene_creato()
    {
        using var t = new TestDb();
        await t.Arrivi.RegistraAsync([Arrivo("d1", "cuscinetto 6204-2RS", "1", fornitore: "Ricambi Rossi srl")], caricaSeCerto: false);
        var a = await t.Db.ArriviEsterni.SingleAsync();
        Assert.True(a.Certo);
        Assert.Equal(StatoArrivoEsterno.DaConfermare, a.Stato); // automatico spento
        await t.Arrivi.ConfermaAsync(a.Id, t.Cuscinetto.Id, 1, t.U1.Id, "anna");
        Assert.True(await t.Db.Fornitori.AnyAsync(f => f.RagioneSociale == "Ricambi Rossi srl"));
    }
}
