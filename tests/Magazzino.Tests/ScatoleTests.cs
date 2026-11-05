using Magazzino.Domain.Scarico;
using Magazzino.Infrastructure.Servizi;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Tests;

public class ScatoleTests
{
    [Theory]
    [InlineData("SC-000012", "SC-000012")]
    [InlineData("sc-000012", "SC-000012")]
    [InlineData("https://magazzino.oliodante.local/t/s/SC-000012", "SC-000012")]
    [InlineData("http://192.168.93.14:5080/t/s/SC-000012?x=1", "SC-000012")]
    [InlineData("  SC-000012\n", "SC-000012")]
    public void Codice_letto_dal_qr(string letto, string atteso) => Assert.Equal(atteso, ScatoleService.CodiceDaQr(letto));

    [Fact]
    public async Task Numerazione_continua_e_scatole_per_giacenze_mancanti()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 5, 4m), new RigaDaRicevere(t.Cuscinetto.Id, t.U2.Id, 2, 4m)], "u");
        var prima = await t.Scatole.CreaAsync(t.Cuscinetto.Id, t.U1.Id);
        Assert.Equal("SC-000001", prima.Codice);

        var nuove = await t.Scatole.CreaPerGiacenzeSenzaScatolaAsync();
        var s = Assert.Single(nuove); // U1 ha gia' la sua scatola
        Assert.Equal("SC-000002", s.Codice);
        Assert.Equal(t.U2.Id, s.UbicazioneId);
        Assert.Empty(await t.Scatole.CreaPerGiacenzeSenzaScatolaAsync());
    }

    [Fact]
    public async Task Scansione_e_prelievo_dalla_scatola()
    {
        using var t = new TestDb();
        await t.Carico.RegistraRicevimentoAsync(t.Fornitore.Id, "DDT", DateTime.Today, [new RigaDaRicevere(t.Cuscinetto.Id, t.U1.Id, 5, 4m)], "u");
        var sc = await t.Scatole.CreaAsync(t.Cuscinetto.Id, t.U1.Id);

        var info = await t.Scatole.TrovaAsync($"https://server/t/s/{sc.Codice}");
        Assert.Equal(5, info.Giacenza);
        Assert.Equal(t.Cuscinetto.Id, info.Ricambio.Id);

        var b1 = await t.Scarico.PrelevaPerLineaAsync(t.Linea.Id, null, MotivoIntervento.Guasto, [new RigaDaPrelevare(info.Ricambio.Id, info.Ubicazione.Id, 2)], "Mario");
        // Secondo prelievo sullo stesso intervento: niente nuovo intervento.
        await t.Scarico.PrelevaPerLineaAsync(t.Linea.Id, null, MotivoIntervento.Guasto, [new RigaDaPrelevare(info.Ricambio.Id, info.Ubicazione.Id, 1)], "Mario", b1.InterventoId);

        Assert.Equal(2, (await t.Scatole.TrovaAsync(sc.Codice)).Giacenza);
        var i = await t.Db.Interventi.Include(x => x.RicambiUsati).SingleAsync();
        Assert.Equal(3, i.RicambiUsati.Sum(r => r.Quantita));
    }

    [Fact]
    public async Task Scatola_dismessa_o_inesistente()
    {
        using var t = new TestDb();
        var sc = await t.Scatole.CreaAsync(t.Cuscinetto.Id, t.U1.Id);
        sc.Attiva = false;
        await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Scatole.TrovaAsync(sc.Codice));
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Scatole.TrovaAsync("SC-999999"));
    }
}
