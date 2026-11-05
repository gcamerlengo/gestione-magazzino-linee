namespace Magazzino.Domain.Anagrafiche;

/// <summary>
/// Scatola fisica in magazzino con un QR attaccato. Contiene un solo ricambio e sta in una ubicazione:
/// scansionando il QR il tecnico apre direttamente il prelievo di quel ricambio da quella ubicazione.
/// </summary>
public class Scatola : Entity
{
    /// <summary>Codice stampato nel QR, es. SC-000123. Non cambia mai, anche se la scatola cambia contenuto.</summary>
    public string Codice { get; set; } = "";
    public int RicambioId { get; set; }
    public Ricambio? Ricambio { get; set; }
    public int UbicazioneId { get; set; }
    public Ubicazione? Ubicazione { get; set; }
    public string Note { get; set; } = "";
    public bool Attiva { get; set; } = true;
}
