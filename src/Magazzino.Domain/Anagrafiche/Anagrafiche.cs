namespace Magazzino.Domain.Anagrafiche;

public class Linea : Entity
{
    public string Codice { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Reparto { get; set; } = "";
    public bool Attiva { get; set; } = true;
    public string Note { get; set; } = "";
    public List<Macchina> Macchine { get; set; } = [];
}

public class Macchina : Entity
{
    public int LineaId { get; set; }
    public Linea? Linea { get; set; }
    public string CodiceAsset { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Costruttore { get; set; } = "";
    public string Modello { get; set; } = "";
    public string Matricola { get; set; } = "";
    public int? AnnoCostruzione { get; set; }
    public int Criticita { get; set; }
    public string DpiRichiesti { get; set; } = "";
    public bool Attiva { get; set; } = true;
    public string Note { get; set; } = "";
    /// <summary>Id nel database di Gestione Manutenzione, per l'importazione.</summary>
    public int? IdOrigine { get; set; }
}

public enum CategoriaRicambio { Altro, Meccanico, Elettrico, Pneumatico, Oleodinamico, Consumo, Guarnizioni }

public class Ricambio : Entity
{
    public string Codice { get; set; } = "";
    public string CodiceCostruttore { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public CategoriaRicambio Categoria { get; set; }
    public string UnitaMisura { get; set; } = "pz";
    /// <summary>Costo medio ponderato, aggiornato a ogni carico.</summary>
    public decimal CostoMedio { get; set; }
    public decimal ScortaMinima { get; set; }
    public decimal PuntoRiordino { get; set; }
    public decimal QuantitaRiordino { get; set; }
    public int LeadTimeGiorni { get; set; }
    public bool Critico { get; set; }
    public bool AlimentareNsfH1 { get; set; }
    public int? FornitorePreferitoId { get; set; }
    public Fornitore? FornitorePreferito { get; set; }
    public int? UbicazionePredefinitaId { get; set; }
    public Ubicazione? UbicazionePredefinita { get; set; }
    public string FotoPath { get; set; } = "";
    public bool Attivo { get; set; } = true;
    public string Note { get; set; } = "";
    public int? IdOrigine { get; set; }
    // Nessuna quantita' qui: la giacenza vive solo in Giacenza (per ubicazione).
}

public class RicambioMacchina
{
    public int MacchinaId { get; set; }
    public Macchina? Macchina { get; set; }
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public decimal QuantitaConsigliata { get; set; } = 1;
    public string Note { get; set; } = "";
}

public class Fornitore : Entity
{
    public string RagioneSociale { get; set; } = "";
    public string PartitaIva { get; set; } = "";
    public string Referente { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string EmailOrdini { get; set; } = "";
    public string CondizioniPagamento { get; set; } = "";
    public bool Attivo { get; set; } = true;
    public string Note { get; set; } = "";
    public int? IdOrigine { get; set; }
}

public class RicambioFornitore
{
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public int FornitoreId { get; set; }
    public Fornitore? Fornitore { get; set; }
    public string CodiceFornitore { get; set; } = "";
    public decimal Prezzo { get; set; }
    public decimal LottoMinimo { get; set; } = 1;
    public int LeadTimeGiorni { get; set; }
}

public class Ubicazione : Entity
{
    public string Codice { get; set; } = "";
    public string Magazzino { get; set; } = "";
    public string Scaffale { get; set; } = "";
    public string Ripiano { get; set; } = "";
    public string Cella { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public bool Attiva { get; set; } = true;
    public int? IdOrigine { get; set; }
}

public enum RuoloUtente { Tecnico, Ufficio, Responsabile, Admin }

public class Utente : Entity
{
    public string Username { get; set; } = "";
    public string NomeVisualizzato { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public RuoloUtente Ruolo { get; set; }
    public bool Attivo { get; set; } = true;
}
