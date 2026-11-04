using Magazzino.Domain.Anagrafiche;

namespace Magazzino.Domain.Scarico;

public enum MotivoIntervento { Guasto, Preventiva, Miglioria, Altro }
public enum StatoBuono { Aperto, Prelevato, Completato, Annullato }

/// <summary>
/// Buono di prelievo. Se ha una linea, il prelievo crea (o usa) l'intervento di manutenzione su quella linea.
/// Senza linea e' uno scarico generico con motivo obbligatorio.
/// </summary>
public class BuonoPrelievo : Entity
{
    public string Numero { get; set; } = "";
    public int? LineaId { get; set; }
    public Linea? Linea { get; set; }
    public int? MacchinaId { get; set; }
    public Macchina? Macchina { get; set; }
    public MotivoIntervento Motivo { get; set; }
    public string MotivoScaricoGenerico { get; set; } = "";
    public StatoBuono Stato { get; set; }
    public string Tecnico { get; set; } = "";
    public int? InterventoId { get; set; }
    public Intervento? Intervento { get; set; }
    public List<RigaBuonoPrelievo> Righe { get; set; } = [];
}

public class RigaBuonoPrelievo
{
    public int Id { get; set; }
    public int BuonoPrelievoId { get; set; }
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public int UbicazioneId { get; set; }
    public decimal Quantita { get; set; }
    public decimal QuantitaResa { get; set; }
}

public enum StatoIntervento { InCorso, Completato, Annullato }
public enum DestinoPezzoSmontato { Nessuno, DaRiparare, DaSmaltire, RientroMagazzino }

/// <summary>Intervento di manutenzione su UNA linea (macchina facoltativa).</summary>
public class Intervento : Entity
{
    public string Codice { get; set; } = "";
    public int LineaId { get; set; }
    public Linea? Linea { get; set; }
    public int? MacchinaId { get; set; }
    public Macchina? Macchina { get; set; }
    public MotivoIntervento Motivo { get; set; }
    public StatoIntervento Stato { get; set; }
    public DateTime Inizio { get; set; } = DateTime.UtcNow;
    public DateTime? Fine { get; set; }
    public decimal OreLavoro { get; set; }
    public string Tecnico { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public DestinoPezzoSmontato PezzoSmontato { get; set; }
    public decimal CostoManodopera { get; set; }
    public decimal CostoEsterni { get; set; }
    public string FotoPath { get; set; } = "";
    public int? IdOrigine { get; set; }
    public List<RicambioUsato> RicambiUsati { get; set; } = [];

    public decimal CostoRicambi => RicambiUsati.Sum(r => r.Quantita * r.CostoUnitario);
}

public class RicambioUsato
{
    public int Id { get; set; }
    public int InterventoId { get; set; }
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public decimal Quantita { get; set; }
    /// <summary>Costo medio al momento del prelievo.</summary>
    public decimal CostoUnitario { get; set; }
    public int MovimentoId { get; set; }
}
