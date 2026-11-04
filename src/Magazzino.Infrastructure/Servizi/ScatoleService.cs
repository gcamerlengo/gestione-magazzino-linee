using Magazzino.Domain.Anagrafiche;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

public record InfoScatola(Scatola Scatola, Ricambio Ricambio, Ubicazione Ubicazione, decimal Giacenza);

public class ScatoleService(MagazzinoDbContext db, Numeratore numeratore)
{
    public const string Serie = "SC";

    public async Task<Scatola> CreaAsync(int ricambioId, int ubicazioneId, string note = "")
    {
        if (!await db.Ricambi.AnyAsync(r => r.Id == ricambioId && r.Attivo)) throw new MagazzinoException("Ricambio inesistente o non attivo.");
        if (!await db.Ubicazioni.AnyAsync(u => u.Id == ubicazioneId && u.Attiva)) throw new MagazzinoException("Ubicazione inesistente o non attiva.");
        // Numerazione unica, senza anno: il codice e' stampato sull'etichetta e non deve cambiare.
        var (_, _, prog) = await numeratore.ProssimoAsync(Serie, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var s = new Scatola { Codice = $"{Serie}-{prog:D6}", RicambioId = ricambioId, UbicazioneId = ubicazioneId, Note = note };
        db.Scatole.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    /// <summary>Una scatola per ogni giacenza che non ne ha ancora una attiva: utile per etichettare il magazzino la prima volta.</summary>
    public async Task<List<Scatola>> CreaPerGiacenzeSenzaScatolaAsync()
    {
        var conScatola = await db.Scatole.Where(s => s.Attiva).Select(s => new { s.RicambioId, s.UbicazioneId }).ToListAsync();
        var giacenze = await db.Giacenze.Where(g => g.Quantita > 0).Select(g => new { g.RicambioId, g.UbicazioneId }).ToListAsync();
        var res = new List<Scatola>();
        foreach (var g in giacenze.Where(g => !conScatola.Contains(g)).OrderBy(g => g.UbicazioneId).ThenBy(g => g.RicambioId))
            res.Add(await CreaAsync(g.RicambioId, g.UbicazioneId));
        return res;
    }

    /// <summary>Legge il codice da un QR: accetta il codice nudo o l'indirizzo completo stampato sull'etichetta.</summary>
    public static string CodiceDaQr(string letto)
    {
        var t = letto.Trim();
        var i = t.LastIndexOf('/');
        if (i >= 0) t = t[(i + 1)..];
        var q = t.IndexOfAny(['?', '#']);
        if (q >= 0) t = t[..q];
        return Uri.UnescapeDataString(t).ToUpperInvariant();
    }

    public async Task<InfoScatola> TrovaAsync(string codiceOQr)
    {
        var codice = CodiceDaQr(codiceOQr);
        var s = await db.Scatole.Include(x => x.Ricambio).Include(x => x.Ubicazione).FirstOrDefaultAsync(x => x.Codice == codice)
                ?? throw new MagazzinoException($"Scatola {codice} non trovata.");
        if (!s.Attiva) throw new MagazzinoException($"La scatola {codice} non e' piu' in uso.");
        var g = await db.Giacenze.Where(x => x.RicambioId == s.RicambioId && x.UbicazioneId == s.UbicazioneId).Select(x => x.Quantita).FirstOrDefaultAsync();
        return new InfoScatola(s, s.Ricambio!, s.Ubicazione!, g);
    }
}
