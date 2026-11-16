using Magazzino.Domain.Anagrafiche;

namespace Magazzino.Domain.Carico;

public enum StatoArrivoEsterno { DaConfermare, Caricato, Scartato }

/// <summary>
/// Materiale arrivato registrato in un altro programma (Risolvo: richiesta materiale chiusa come "ricevuto").
/// Diventa un carico di magazzino quando l'ufficio lo conferma, o da solo se l'articolo e' riconosciuto con certezza.
/// </summary>
public class ArrivoEsterno : Entity
{
    public string Sorgente { get; set; } = "Risolvo";
    /// <summary>Identificativo nel programma di origine: impedisce di caricare due volte lo stesso arrivo.</summary>
    public string IdEsterno { get; set; } = "";
    public string Numero { get; set; } = "";
    /// <summary>Testo libero scritto in Risolvo (descrizione, codice o modello).</summary>
    public string Materiale { get; set; } = "";
    public string QuantitaTesto { get; set; } = "";
    public string Luogo { get; set; } = "";
    public string FornitoreTesto { get; set; } = "";
    public string RiferimentoOrdine { get; set; } = "";
    public DateTime? RicevutoIl { get; set; }

    public StatoArrivoEsterno Stato { get; set; }
    // Proposta (o scelta confermata) per il carico.
    public int? RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public decimal? Quantita { get; set; }
    public int? UbicazioneId { get; set; }
    public Ubicazione? Ubicazione { get; set; }
    /// <summary>Come e' stato riconosciuto l'articolo (per far capire la proposta all'ufficio).</summary>
    public string ComeRiconosciuto { get; set; } = "";
    public bool Certo { get; set; }

    public int? RicevimentoId { get; set; }
    public Ricevimento? Ricevimento { get; set; }
    public bool CaricatoInAutomatico { get; set; }
    public string GestitoDa { get; set; } = "";
    public DateTime? GestitoIl { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Abbinamento imparato: "questo testo di Risolvo e' questo ricambio". Si crea quando l'ufficio conferma un arrivo.</summary>
public class AbbinamentoMateriale : Entity
{
    public string TestoNormalizzato { get; set; } = "";
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
}

/// <summary>Impostazioni modificabili dal programma (chiave/valore).</summary>
public class Impostazione
{
    public string Chiave { get; set; } = "";
    public string Valore { get; set; } = "";
    public DateTime AggiornataIl { get; set; } = DateTime.UtcNow;
}
