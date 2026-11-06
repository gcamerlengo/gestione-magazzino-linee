using System.Security.Cryptography;
using System.Text;
using Magazzino.Domain.Anagrafiche;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

/// <summary>
/// Utenti e password. Le password sono salvate solo come hash (PBKDF2 di ASP.NET Core Identity).
/// I tecnici possono usare un PIN di almeno 4 cifre; ufficio, responsabili e amministratori almeno 8 caratteri.
/// </summary>
public class UtentiService(MagazzinoDbContext db)
{
    private static readonly PasswordHasher<Utente> Hasher = new();

    public static int LunghezzaMinima(RuoloUtente ruolo) => ruolo == RuoloUtente.Tecnico ? 4 : 8;

    /// <summary>Impronta corta dell'hash: cambia quando cambia la password, e chiude le sessioni aperte con quella vecchia.</summary>
    public static string Impronta(Utente u) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{u.PasswordHash}|{u.Ruolo}|{u.Attivo}")))[..16];

    public static string NormalizzaUsername(string username) => username.Trim().ToLowerInvariant();

    public Task<bool> EsisteAdminAsync() => db.Utenti.AnyAsync(u => u.Attivo && u.Ruolo == RuoloUtente.Admin);

    /// <summary>Restituisce l'utente se nome e password sono giusti e l'utente e' attivo, altrimenti null.</summary>
    public async Task<Utente?> VerificaAsync(string username, string password)
    {
        var nome = NormalizzaUsername(username);
        var u = await db.Utenti.FirstOrDefaultAsync(x => x.Username == nome);
        if (u is null || !u.Attivo || string.IsNullOrEmpty(u.PasswordHash))
        {
            Hasher.HashPassword(new Utente(), password); // stesso tempo di risposta anche se l'utente non esiste
            return null;
        }
        var esito = Hasher.VerifyHashedPassword(u, u.PasswordHash, password);
        if (esito == PasswordVerificationResult.Failed) return null;
        if (esito == PasswordVerificationResult.SuccessRehashNeeded)
        {
            u.PasswordHash = Hasher.HashPassword(u, password);
            await db.SaveChangesAsync();
        }
        return u;
    }

    public async Task<Utente> CreaAsync(string username, string nomeVisualizzato, RuoloUtente ruolo, string password)
    {
        var nome = NormalizzaUsername(username);
        if (nome.Length < 2 || nome.Any(char.IsWhiteSpace)) throw new MagazzinoException("Il nome utente deve avere almeno 2 caratteri e nessuno spazio.");
        if (string.IsNullOrWhiteSpace(nomeVisualizzato)) throw new MagazzinoException("Scrivi nome e cognome.");
        if (await db.Utenti.AnyAsync(x => x.Username == nome)) throw new MagazzinoException($"Il nome utente '{nome}' esiste gia'.");
        ControllaPassword(password, ruolo);
        var u = new Utente { Username = nome, NomeVisualizzato = nomeVisualizzato.Trim(), Ruolo = ruolo, Attivo = true };
        u.PasswordHash = Hasher.HashPassword(u, password);
        db.Utenti.Add(u);
        await db.SaveChangesAsync();
        return u;
    }

    /// <summary>Crea il primo amministratore. Possibile solo finche' non esiste nessun amministratore attivo.</summary>
    public async Task<Utente> CreaPrimoAdminAsync(string username, string nomeVisualizzato, string password)
    {
        if (await EsisteAdminAsync()) throw new MagazzinoException("L'amministratore esiste gia'.");
        return await CreaAsync(username, nomeVisualizzato, RuoloUtente.Admin, password);
    }

    public async Task ModificaAsync(int id, string nomeVisualizzato, RuoloUtente ruolo, bool attivo)
    {
        var u = await db.Utenti.FindAsync(id) ?? throw new MagazzinoException("Utente non trovato.");
        if (string.IsNullOrWhiteSpace(nomeVisualizzato)) throw new MagazzinoException("Scrivi nome e cognome.");
        var perdeAdmin = u.Ruolo == RuoloUtente.Admin && u.Attivo && (ruolo != RuoloUtente.Admin || !attivo);
        if (perdeAdmin && !await db.Utenti.AnyAsync(x => x.Id != id && x.Attivo && x.Ruolo == RuoloUtente.Admin))
            throw new MagazzinoException("Deve restare almeno un amministratore attivo.");
        u.NomeVisualizzato = nomeVisualizzato.Trim();
        u.Ruolo = ruolo;
        u.Attivo = attivo;
        await db.SaveChangesAsync();
    }

    public async Task ImpostaPasswordAsync(int id, string password)
    {
        var u = await db.Utenti.FindAsync(id) ?? throw new MagazzinoException("Utente non trovato.");
        ControllaPassword(password, u.Ruolo);
        u.PasswordHash = Hasher.HashPassword(u, password);
        await db.SaveChangesAsync();
    }

    /// <summary>Cambio password fatto dall'utente stesso: serve la password attuale.</summary>
    public async Task CambiaPasswordAsync(int id, string attuale, string nuova)
    {
        var u = await db.Utenti.FindAsync(id) ?? throw new MagazzinoException("Utente non trovato.");
        if (Hasher.VerifyHashedPassword(u, u.PasswordHash, attuale) == PasswordVerificationResult.Failed)
            throw new MagazzinoException("La password attuale non e' giusta.");
        await ImpostaPasswordAsync(id, nuova);
    }

    private static void ControllaPassword(string password, RuoloUtente ruolo)
    {
        var min = LunghezzaMinima(ruolo);
        if (string.IsNullOrEmpty(password) || password.Length < min)
            throw new MagazzinoException(ruolo == RuoloUtente.Tecnico
                ? $"Il PIN deve avere almeno {min} caratteri."
                : $"La password deve avere almeno {min} caratteri.");
    }
}
