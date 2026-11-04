using Magazzino.Domain.Anagrafiche;

namespace Magazzino.Domain.Carico;

public enum StatoRichiesta { Aperta, InOrdine, Evasa, Annullata }

/// <summary>Richiesta di acquisto (RDA): manuale o generata dalla proposta di riordino.</summary>
public class RichiestaAcquisto : Entity
{
    public string Numero { get; set; } = "";
    public DateTime Data { get; set; } = DateTime.UtcNow;
    public string Richiedente { get; set; } = "";
    public StatoRichiesta Stato { get; set; }
    public bool DaRiordinoAutomatico { get; set; }
    public string Note { get; set; } = "";
    public List<RigaRichiestaAcquisto> Righe { get; set; } = [];
}

public class RigaRichiestaAcquisto
{
    public int Id { get; set; }
    public int RichiestaAcquistoId { get; set; }
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public decimal Quantita { get; set; }
    public string Motivo { get; set; } = "";
    public int? LineaDestinazioneId { get; set; }
    public int? RigaOrdineFornitoreId { get; set; }
}

public enum StatoOrdineFornitore { Bozza, DaApprovare, Inviato, Confermato, RicevutoInParte, Ricevuto, Chiuso, Annullato }

public class OrdineFornitore : Entity
{
    public string Numero { get; set; } = "";
    public int FornitoreId { get; set; }
    public Fornitore? Fornitore { get; set; }
    public DateTime DataOrdine { get; set; } = DateTime.UtcNow;
    public DateTime? DataConsegnaPrevista { get; set; }
    public StatoOrdineFornitore Stato { get; set; }
    public string ApprovatoDa { get; set; } = "";
    public DateTime? ApprovatoIl { get; set; }
    public string FilePdf { get; set; } = "";
    public string Note { get; set; } = "";
    public int? IdOrigine { get; set; }
    public List<RigaOrdineFornitore> Righe { get; set; } = [];

    public decimal Totale => Righe.Sum(r => r.Quantita * r.PrezzoUnitario);
}

public class RigaOrdineFornitore
{
    public int Id { get; set; }
    public int OrdineFornitoreId { get; set; }
    public OrdineFornitore? Ordine { get; set; }
    public int NumeroRiga { get; set; }
    public int? RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public string CodiceFornitore { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public string UnitaMisura { get; set; } = "pz";
    public decimal Quantita { get; set; }
    public decimal QuantitaRicevuta { get; set; }
    public decimal PrezzoUnitario { get; set; }

    public decimal Residuo => Math.Max(0, Quantita - QuantitaRicevuta);
}

/// <summary>Ricevimento merce da DDT: l'unico documento di carico che muove la giacenza.</summary>
public class Ricevimento : Entity
{
    public string Numero { get; set; } = "";
    public int FornitoreId { get; set; }
    public Fornitore? Fornitore { get; set; }
    public string NumeroDdt { get; set; } = "";
    public DateTime DataDdt { get; set; }
    public DateTime RicevutoIl { get; set; } = DateTime.UtcNow;
    public string RicevutoDa { get; set; } = "";
    public string Note { get; set; } = "";
    public List<RigaRicevimento> Righe { get; set; } = [];
}

public class RigaRicevimento
{
    public int Id { get; set; }
    public int RicevimentoId { get; set; }
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public int? RigaOrdineFornitoreId { get; set; }
    public RigaOrdineFornitore? RigaOrdine { get; set; }
    public int UbicazioneId { get; set; }
    public decimal Quantita { get; set; }
    public decimal PrezzoUnitario { get; set; }
}
