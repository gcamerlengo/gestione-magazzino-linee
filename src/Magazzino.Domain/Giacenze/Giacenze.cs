using Magazzino.Domain.Anagrafiche;

namespace Magazzino.Domain.Giacenze;

public enum TipoMovimento
{
    CaricoFornitore,
    ScaricoIntervento,
    ScaricoGenerico,
    Reso,
    Trasferimento,
    RettificaInventario,
    Apertura,
    Storno
}

/// <summary>Movimento di magazzino: mai cancellato, si storna con un movimento opposto.</summary>
public class Movimento
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public int Anno { get; set; }
    public int Progressivo { get; set; }
    public DateTime Data { get; set; } = DateTime.UtcNow;
    public string Utente { get; set; } = "";
    public TipoMovimento Tipo { get; set; }
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public int? UbicazioneDaId { get; set; }
    public int? UbicazioneAId { get; set; }
    /// <summary>Sempre positiva: il verso e' dato da UbicazioneDa / UbicazioneA.</summary>
    public decimal Quantita { get; set; }
    public decimal CostoUnitario { get; set; }
    public string TipoDocumento { get; set; } = "";
    public int? DocumentoId { get; set; }
    public int? LineaId { get; set; }
    public int? InterventoId { get; set; }
    public int? StornaMovimentoId { get; set; }
    public bool Stornato { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Giacenza per ricambio e ubicazione: aggiornata solo nella stessa transazione del movimento.</summary>
public class Giacenza
{
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public int UbicazioneId { get; set; }
    public Ubicazione? Ubicazione { get; set; }
    public decimal Quantita { get; set; }
    public DateTime? UltimoMovimento { get; set; }
    public uint Versione { get; set; }
}
