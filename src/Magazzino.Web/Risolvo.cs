using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Magazzino.Domain.Carico;
using Magazzino.Infrastructure;
using Magazzino.Infrastructure.Servizi;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Web;

/// <summary>Impostazioni del collegamento con Risolvo (programma guasti/materiali). La password e' salvata cifrata.</summary>
public record ImpostazioniRisolvo(bool Attivo, string Indirizzo, string Email, string Password, DateTime DaData, bool CaricaSeCerto);

public record EsitoSincronizzazione(DateTime Quando, bool Riuscita, string Messaggio);

/// <summary>
/// Legge da Risolvo le richieste materiale chiuse come "ricevuto" e le passa al magazzino come arrivi.
/// Solo letture verso Risolvo: non modifica nulla di la'.
/// </summary>
public class CollegamentoRisolvo(IServiceScopeFactory scopes, IHttpClientFactory http, IDataProtectionProvider dp, ILogger<CollegamentoRisolvo> log)
{
    private const string Prefisso = "Risolvo.";
    private readonly IDataProtector protezione = dp.CreateProtector("Magazzino.Risolvo.Password");
    private readonly SemaphoreSlim inCorso = new(1, 1);

    public EsitoSincronizzazione? UltimoEsito { get; private set; }

    public async Task<ImpostazioniRisolvo> LeggiAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MagazzinoDbContext>();
        var v = await db.Impostazioni.Where(i => i.Chiave.StartsWith(Prefisso)).ToDictionaryAsync(i => i.Chiave[Prefisso.Length..], i => i.Valore);
        string pw = "";
        if (v.TryGetValue("Password", out var cifrata) && cifrata != "")
        {
            try { pw = protezione.Unprotect(cifrata); } catch (Exception) { pw = ""; } // chiavi cambiate: va reinserita
        }
        return new ImpostazioniRisolvo(
            v.GetValueOrDefault("Attivo") == "1",
            v.GetValueOrDefault("Indirizzo") ?? "http://192.168.1.87:3001",
            v.GetValueOrDefault("Email") ?? "",
            pw,
            DateTime.TryParse(v.GetValueOrDefault("DaData"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d) ? d.Date : DateTime.UtcNow.Date,
            v.GetValueOrDefault("CaricaSeCerto", "1") == "1");
    }

    public async Task SalvaAsync(ImpostazioniRisolvo i)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MagazzinoDbContext>();
        var valori = new Dictionary<string, string>
        {
            ["Attivo"] = i.Attivo ? "1" : "0",
            ["Indirizzo"] = i.Indirizzo.Trim().TrimEnd('/'),
            ["Email"] = i.Email.Trim(),
            ["Password"] = i.Password == "" ? "" : protezione.Protect(i.Password),
            ["DaData"] = i.DaData.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["CaricaSeCerto"] = i.CaricaSeCerto ? "1" : "0",
        };
        foreach (var (k, val) in valori)
        {
            var riga = await db.Impostazioni.FindAsync(Prefisso + k);
            if (riga is null) db.Impostazioni.Add(new Impostazione { Chiave = Prefisso + k, Valore = val });
            else { riga.Valore = val; riga.AggiornataIl = DateTime.UtcNow; }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Prova solo l'accesso a Risolvo, senza registrare nulla.</summary>
    public async Task<string> ProvaAsync(ImpostazioniRisolvo i, CancellationToken ct = default)
    {
        using var client = await ClientAsync(i, ct);
        var lista = await LeggiRicevuteAsync(client, i.DaData, ct);
        return $"Collegamento riuscito: {lista.Count} richieste ricevute dal {i.DaData:dd/MM/yyyy}.";
    }

    public async Task<EsitoSincronizzazione> SincronizzaAsync(CancellationToken ct = default)
    {
        if (!await inCorso.WaitAsync(0, ct)) return UltimoEsito ?? new(DateTime.UtcNow, true, "Sincronizzazione gia' in corso.");
        try
        {
            var i = await LeggiAsync();
            if (!i.Attivo) return UltimoEsito = new(DateTime.UtcNow, true, "Collegamento non attivo.");
            using var client = await ClientAsync(i, ct);
            var lista = await LeggiRicevuteAsync(client, i.DaData, ct);
            await using var scope = scopes.CreateAsyncScope();
            var (nuovi, caricati) = await scope.ServiceProvider.GetRequiredService<ArriviService>().RegistraAsync(lista, i.CaricaSeCerto);
            return UltimoEsito = new(DateTime.UtcNow, true, $"{lista.Count} richieste ricevute lette, {nuovi} nuove, {caricati} caricate in automatico.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Sincronizzazione Risolvo non riuscita");
            return UltimoEsito = new(DateTime.UtcNow, false, Spiega(ex));
        }
        finally { inCorso.Release(); }
    }

    public static string Spiega(Exception ex) => ex switch
    {
        MagazzinoException m => m.Message,
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } =>
            "Risolvo ha rifiutato l'accesso: controlla email e password, e che l'utente abbia il permesso materiali.",
        HttpRequestException h => $"Risolvo non raggiungibile ({h.InnerException?.Message ?? h.Message}). Controlla l'indirizzo e che il server sia acceso.",
        JsonException => "Risolvo ha risposto in un formato non previsto: controlla l'indirizzo.",
        TaskCanceledException => "Risolvo non ha risposto in tempo.",
        _ => ex.Message,
    };

    private async Task<HttpClient> ClientAsync(ImpostazioniRisolvo i, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(i.Indirizzo) || string.IsNullOrWhiteSpace(i.Email) || string.IsNullOrEmpty(i.Password))
            throw new MagazzinoException("Mancano indirizzo, email o password di Risolvo.");
        var client = http.CreateClient("risolvo");
        client.BaseAddress = new Uri(i.Indirizzo.TrimEnd('/') + "/");
        // corpo con lunghezza fissa: alcuni server non accettano l'invio "a pezzi" (chunked)
        using var corpo = new StringContent(JsonSerializer.Serialize(new { email = i.Email, password = i.Password }), System.Text.Encoding.UTF8, "application/json");
        var r = await client.PostAsync("auth/login", corpo, ct);
        r.EnsureSuccessStatusCode();
        var json = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        var token = json.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(token)) throw new MagazzinoException("Risolvo non ha restituito il token di accesso.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Richieste materiale in stato "ricevuto" (Chiuso · ricevuto), dalla data indicata in poi.</summary>
    public static async Task<List<ArrivoDaRisolvo>> LeggiRicevuteAsync(HttpClient client, DateTime daData, CancellationToken ct)
    {
        var tutte = new List<ArrivoDaRisolvo>();
        for (var pagina = 1; pagina <= 200; pagina++)
        {
            var url = $"ops-api/materials/dashboard?scope=requests&status=received&date_from={daData:yyyy-MM-dd}&page={pagina}";
            var json = await client.GetFromJsonAsync<JsonElement>(url, ct);
            if (!json.TryGetProperty("requests", out var richieste) || richieste.ValueKind != JsonValueKind.Array) break;
            foreach (var r in richieste.EnumerateArray())
            {
                if (Testo(r, "status") is var s && s != "" && s != "received") continue;
                var a = Converti(r);
                if (a.RicevutoIl is DateTime quando && quando.Date < daData.Date) continue;
                tutte.Add(a);
            }
            var pagine = json.TryGetProperty("pages", out var p) && p.TryGetInt32(out var n) ? n : 1;
            if (pagina >= pagine) break;
        }
        return tutte;
    }

    public static ArrivoDaRisolvo Converti(JsonElement r)
    {
        var numero = Testo(r, "number");
        if (numero != "" && !numero.StartsWith("MAT", StringComparison.OrdinalIgnoreCase)) numero = "MAT-" + numero;
        var data = Testo(r, "received_on") is var ro && ro != "" ? ro : Testo(r, "received_at");
        DateTime? ricevuto = DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;
        return new ArrivoDaRisolvo(Testo(r, "id"), numero, Testo(r, "material"), Testo(r, "quantity"), Testo(r, "location"),
            Testo(r, "supplier"), Testo(r, "order_reference"), ricevuto);
    }

    private static string Testo(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.Object => v.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
            _ => "",
        } : "";
}

/// <summary>Controlla Risolvo ogni 5 minuti.</summary>
public class SincronizzazioneRisolvo(CollegamentoRisolvo collegamento) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stop); // lascia partire il programma
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do { await collegamento.SincronizzaAsync(stop); }
        while (await timer.WaitForNextTickAsync(stop));
    }
}
