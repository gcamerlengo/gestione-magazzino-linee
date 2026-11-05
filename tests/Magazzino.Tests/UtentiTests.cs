using Magazzino.Domain.Anagrafiche;
using Magazzino.Infrastructure.Servizi;

namespace Magazzino.Tests;

public class UtentiTests
{
    [Fact]
    public async Task Accesso_con_password_giusta_e_sbagliata()
    {
        using var t = new TestDb();
        var u = await t.Utenti.CreaAsync(" Mario.Rossi ", "Mario Rossi", RuoloUtente.Tecnico, "1234");
        Assert.Equal("mario.rossi", u.Username);
        Assert.DoesNotContain("1234", u.PasswordHash);

        Assert.NotNull(await t.Utenti.VerificaAsync("MARIO.ROSSI", "1234"));
        Assert.Null(await t.Utenti.VerificaAsync("mario.rossi", "4321"));
        Assert.Null(await t.Utenti.VerificaAsync("nessuno", "1234"));

        await t.Utenti.ModificaAsync(u.Id, "Mario Rossi", RuoloUtente.Tecnico, attivo: false);
        Assert.Null(await t.Utenti.VerificaAsync("mario.rossi", "1234"));
    }

    [Fact]
    public async Task Regole_su_password_e_nomi()
    {
        using var t = new TestDb();
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Utenti.CreaAsync("ufficio1", "Anna", RuoloUtente.Ufficio, "1234")); // ufficio: almeno 8
        await t.Utenti.CreaAsync("ufficio1", "Anna", RuoloUtente.Ufficio, "password1");
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Utenti.CreaAsync("UFFICIO1", "Altra", RuoloUtente.Tecnico, "1234")); // doppione
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Utenti.CreaAsync("con spazio", "X", RuoloUtente.Tecnico, "1234"));
    }

    [Fact]
    public async Task Resta_sempre_un_amministratore()
    {
        using var t = new TestDb();
        Assert.False(await t.Utenti.EsisteAdminAsync());
        var admin = await t.Utenti.CreaPrimoAdminAsync("admin", "Gianluca", "password-lunga");
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Utenti.CreaPrimoAdminAsync("admin2", "Altro", "password-lunga"));
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Utenti.ModificaAsync(admin.Id, "Gianluca", RuoloUtente.Ufficio, true));
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Utenti.ModificaAsync(admin.Id, "Gianluca", RuoloUtente.Admin, false));

        await t.Utenti.CreaAsync("admin2", "Secondo", RuoloUtente.Admin, "password-lunga");
        await t.Utenti.ModificaAsync(admin.Id, "Gianluca", RuoloUtente.Responsabile, true); // ora si puo'
    }

    [Fact]
    public async Task Cambio_password_chiude_le_sessioni_vecchie()
    {
        using var t = new TestDb();
        var u = await t.Utenti.CreaAsync("luca", "Luca", RuoloUtente.Tecnico, "1111");
        var prima = UtentiService.Impronta(u);
        await Assert.ThrowsAsync<MagazzinoException>(() => t.Utenti.CambiaPasswordAsync(u.Id, "0000", "2222"));
        await t.Utenti.CambiaPasswordAsync(u.Id, "1111", "2222");
        Assert.NotEqual(prima, UtentiService.Impronta(u));
        Assert.NotNull(await t.Utenti.VerificaAsync("luca", "2222"));
        Assert.Null(await t.Utenti.VerificaAsync("luca", "1111"));
    }
}
