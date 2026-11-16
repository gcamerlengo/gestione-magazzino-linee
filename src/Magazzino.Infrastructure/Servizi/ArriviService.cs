using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Magazzino.Domain.Anagrafiche;
using Magazzino.Domain.Carico;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

/// <summary>Una richiesta materiale chiusa come "ricevuto" nel programma Risolvo.</summary>
public record ArrivoDaRisolvo(string Id, string Numero, string Materiale, string Quantita, string Luogo,
    string Fornitore, string RiferimentoOrdine, DateTime? RicevutoIl);

public record PropostaArticolo(Ricambio? Ricambio, bool Certo, string Come);

/// <summary>
/// Arrivi da programmi esterni (Risolvo). In Risolvo materiale e quantita' sono testo libero:
/// qui si propone il ricambio e la quantita', e il carico si fa alla conferma dell'ufficio
/// (o da solo, se attivato, quando l'articolo e' riconosciuto con certezza).
/// </summary>
public partial class ArriviService(MagazzinoDbContext db, CaricoService carico)
{
    public const string UtenteAutomatico = "Risolvo (automatico)";
    public const string FornitoreNonIndicato = "Fornitore non indicato (Risolvo)";

    /// <summary>Registra gli arrivi nuovi (gli altri li ignora). Restituisce quanti sono nuovi e quanti caricati da soli.</summary>
    public async Task<(int Nuovi, int Caricati)> RegistraAsync(IEnumerable<ArrivoDaRisolvo> arrivi, bool caricaSeCerto)
    {
        int nuovi = 0, caricati = 0;
        foreach (var a in arrivi)
        {
            if (string.IsNullOrWhiteSpace(a.Id)) continue;
            if (await db.ArriviEsterni.AnyAsync(x => x.Sorgente == "Risolvo" && x.IdEsterno == a.Id)) continue;

            var proposta = await ProponiAsync(a.Materiale);
            var qta = LeggiQuantita(a.Quantita);
            var arrivo = new ArrivoEsterno
            {
                IdEsterno = a.Id, Numero = a.Numero, Materiale = a.Materiale.Trim(), QuantitaTesto = a.Quantita.Trim(),
                Luogo = a.Luogo.Trim(), FornitoreTesto = a.Fornitore.Trim(), RiferimentoOrdine = a.RiferimentoOrdine.Trim(),
                RicevutoIl = a.RicevutoIl is DateTime d ? OrdiniService.Utc(d) : null,
                RicambioId = proposta.Ricambio?.Id, Quantita = qta, Certo = proposta.Certo, ComeRiconosciuto = proposta.Come,
                UbicazioneId = proposta.Ricambio is null ? null : await UbicazioneProbabileAsync(proposta.Ricambio),
            };
            db.ArriviEsterni.Add(arrivo);
            await db.SaveChangesAsync();
            nuovi++;

            if (caricaSeCerto && arrivo.Certo && arrivo.Quantita > 0 && arrivo.UbicazioneId is not null)
            {
                await ConfermaAsync(arrivo.Id, arrivo.RicambioId!.Value, arrivo.Quantita!.Value, arrivo.UbicazioneId.Value, UtenteAutomatico, ricorda: false);
                caricati++;
            }
        }
        return (nuovi, caricati);
    }

    /// <summary>Carica in magazzino l'arrivo con il ricambio, la quantita' e l'ubicazione scelti.</summary>
    public async Task<Ricevimento> ConfermaAsync(int arrivoId, int ricambioId, decimal quantita, int ubicazioneId, string utente, bool ricorda = true)
    {
        var a = await db.ArriviEsterni.FindAsync(arrivoId) ?? throw new MagazzinoException("Arrivo non trovato.");
        if (a.Stato != StatoArrivoEsterno.DaConfermare) throw new MagazzinoException($"L'arrivo {a.Numero} e' gia' stato gestito.");
        if (quantita <= 0) throw new MagazzinoException("La quantita' deve essere maggiore di zero.");
        var ric = await db.Ricambi.FindAsync(ricambioId) ?? throw new MagazzinoException("Ricambio non trovato.");

        var fornitoreId = await FornitoreAsync(a.FornitoreTesto, ric);
        var prezzo = await db.RicambiFornitori.Where(x => x.RicambioId == ric.Id && x.FornitoreId == fornitoreId && x.Prezzo > 0)
            .Select(x => (decimal?)x.Prezzo).FirstOrDefaultAsync() ?? ric.CostoMedio;
        var ddt = string.IsNullOrWhiteSpace(a.RiferimentoOrdine) ? a.Numero : a.RiferimentoOrdine;
        var r = await carico.RegistraRicevimentoAsync(fornitoreId, ddt, a.RicevutoIl ?? DateTime.UtcNow,
            [new RigaDaRicevere(ric.Id, ubicazioneId, quantita, prezzo)], utente,
            $"Da Risolvo {a.Numero}: {a.Materiale} ({a.QuantitaTesto})");

        a.Stato = StatoArrivoEsterno.Caricato;
        a.RicambioId = ric.Id; a.Quantita = quantita; a.UbicazioneId = ubicazioneId;
        a.RicevimentoId = r.Id; a.GestitoDa = utente; a.GestitoIl = DateTime.UtcNow;
        a.CaricatoInAutomatico = utente == UtenteAutomatico;
        if (ricorda)
        {
            var testo = Normalizza(a.Materiale);
            var ab = await db.AbbinamentiMateriale.FirstOrDefaultAsync(x => x.TestoNormalizzato == testo);
            if (ab is null) db.AbbinamentiMateriale.Add(new AbbinamentoMateriale { TestoNormalizzato = testo, RicambioId = ric.Id });
            else ab.RicambioId = ric.Id;
        }
        await db.SaveChangesAsync();
        return r;
    }

    /// <summary>L'arrivo non va in magazzino (es. materiale di consumo, servizio, gia' caricato a mano).</summary>
    public async Task ScartaAsync(int arrivoId, string motivo, string utente)
    {
        var a = await db.ArriviEsterni.FindAsync(arrivoId) ?? throw new MagazzinoException("Arrivo non trovato.");
        if (a.Stato != StatoArrivoEsterno.DaConfermare) throw new MagazzinoException($"L'arrivo {a.Numero} e' gia' stato gestito.");
        a.Stato = StatoArrivoEsterno.Scartato; a.Note = motivo.Trim(); a.GestitoDa = utente; a.GestitoIl = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Cerca il ricambio dal testo libero. Certo: abbinamento gia' confermato in passato, oppure un solo ricambio
    /// il cui codice (o codice costruttore o codice del fornitore) compare nel testo. Altrimenti una proposta da controllare.
    /// </summary>
    public async Task<PropostaArticolo> ProponiAsync(string materiale)
    {
        var testo = Normalizza(materiale);
        if (testo == "") return new(null, false, "");

        var ab = await db.AbbinamentiMateriale.Include(x => x.Ricambio).FirstOrDefaultAsync(x => x.TestoNormalizzato == testo);
        if (ab?.Ricambio is { Attivo: true }) return new(ab.Ricambio, true, "abbinamento gia' confermato");

        var compatto = Compatta(materiale);
        var candidati = new Dictionary<int, (Ricambio R, string Come)>();
        var ricambi = await db.Ricambi.Where(r => r.Attivo).ToListAsync();
        foreach (var r in ricambi)
        {
            if (ContieneCodice(compatto, r.Codice)) candidati.TryAdd(r.Id, (r, $"codice {r.Codice} nel testo"));
            else if (ContieneCodice(compatto, r.CodiceCostruttore)) candidati.TryAdd(r.Id, (r, $"codice costruttore {r.CodiceCostruttore} nel testo"));
        }
        var codiciFornitore = await db.RicambiFornitori.Include(x => x.Ricambio).Where(x => x.CodiceFornitore != "" && x.Ricambio!.Attivo).ToListAsync();
        foreach (var cf in codiciFornitore.Where(cf => ContieneCodice(compatto, cf.CodiceFornitore)))
            candidati.TryAdd(cf.RicambioId, (cf.Ricambio!, $"codice fornitore {cf.CodiceFornitore} nel testo"));

        // Se un codice e' contenuto in un altro piu' lungo (es. 6204 e 6204-2RS), vale il piu' lungo.
        if (candidati.Count > 1)
        {
            var lunghezza = candidati.ToDictionary(c => c.Key, c => Compatta(c.Value.R.Codice).Length);
            var max = lunghezza.Values.Max();
            var migliori = candidati.Where(c => lunghezza[c.Key] == max).ToList();
            if (migliori.Count == 1) return new(migliori[0].Value.R, true, migliori[0].Value.Come);
            return new(migliori[0].Value.R, false, $"piu' ricambi possibili ({string.Join(", ", migliori.Select(m => m.Value.R.Codice))})");
        }
        if (candidati.Count == 1) { var c = candidati.Values.First(); return new(c.R, true, c.Come); }

        // Nessun codice: il ricambio con piu' parole della descrizione in comune (almeno 2), da controllare.
        var parole = Parole(testo);
        var migliore = ricambi.Select(r => (R: r, N: Parole(Normalizza(r.Descrizione)).Intersect(parole).Count()))
            .Where(x => x.N >= 2).OrderByDescending(x => x.N).FirstOrDefault();
        return migliore.R is null ? new(null, false, "nessun ricambio riconosciuto") : new(migliore.R, false, $"descrizione simile ({migliore.N} parole)");
    }

    /// <summary>"2 pezzi" -> 2, "1,5 kg" -> 1.5, "n. 3" -> 3. Null se non c'e' un numero.</summary>
    public static decimal? LeggiQuantita(string testo)
    {
        var m = Numero().Match(testo ?? "");
        return m.Success && decimal.TryParse(m.Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var q) && q > 0 ? q : null;
    }

    public static string Normalizza(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in (s ?? "").ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Compatta(string s) => new((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    // Il codice deve avere almeno 4 caratteri e una cifra, per non riconoscere parole comuni come codici.
    private static bool ContieneCodice(string testoCompatto, string codice)
    {
        var c = Compatta(codice);
        return c.Length >= 4 && c.Any(char.IsDigit) && testoCompatto.Contains(c, StringComparison.Ordinal);
    }

    private static HashSet<string> Parole(string normalizzato) =>
        normalizzato.Split(' ').Where(p => p.Length >= 3).ToHashSet();

    // Ubicazione predefinita del ricambio, altrimenti quella dove ce n'e' di piu'.
    private async Task<int?> UbicazioneProbabileAsync(Ricambio r)
    {
        if (r.UbicazionePredefinitaId is int u) return u;
        var giacenze = await db.Giacenze.Where(g => g.RicambioId == r.Id).ToListAsync();
        return giacenze.OrderByDescending(g => g.Quantita).Select(g => (int?)g.UbicazioneId).FirstOrDefault();
    }

    private async Task<int> FornitoreAsync(string nome, Ricambio r)
    {
        var n = nome.Trim();
        if (n != "")
        {
            var f = await db.Fornitori.FirstOrDefaultAsync(x => x.RagioneSociale.ToLower() == n.ToLower());
            if (f is not null) return f.Id;
        }
        if (n == "" && r.FornitorePreferitoId is int pref) return pref;
        var nomeNuovo = n == "" ? FornitoreNonIndicato : n;
        var esistente = await db.Fornitori.FirstOrDefaultAsync(x => x.RagioneSociale == nomeNuovo);
        if (esistente is not null) return esistente.Id;
        var nuovo = new Fornitore { RagioneSociale = nomeNuovo, Note = "Creato da un arrivo di Risolvo" };
        db.Fornitori.Add(nuovo);
        await db.SaveChangesAsync();
        return nuovo.Id;
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex Numero();
}
