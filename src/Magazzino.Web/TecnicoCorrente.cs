using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Magazzino.Web;

/// <summary>
/// Nome del tecnico ricordato sul telefono (memoria del browser, cifrata dal server).
/// Segnaposto finche' non arrivano gli accessi con utente e password.
/// </summary>
public class TecnicoCorrente(ProtectedLocalStorage storage)
{
    private const string Chiave = "tecnico";
    public string? Nome { get; private set; }
    public bool Caricato { get; private set; }

    /// <summary>Da chiamare in OnAfterRenderAsync: la memoria del browser non e' disponibile prima.</summary>
    public async Task CaricaAsync()
    {
        if (Caricato) return;
        try
        {
            var r = await storage.GetAsync<string>(Chiave);
            Nome = r.Success ? r.Value : null;
        }
        catch (Exception) { Nome = null; } // memoria non leggibile (es. chiavi cambiate): si richiede il nome
        Caricato = true;
    }

    public async Task ImpostaAsync(string nome)
    {
        Nome = nome.Trim();
        await storage.SetAsync(Chiave, Nome);
    }
}
