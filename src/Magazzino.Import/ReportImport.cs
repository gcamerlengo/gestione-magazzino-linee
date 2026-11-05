using System.Globalization;
using System.Text;

namespace Magazzino.Import;

public class ReportImport
{
    public List<(string Cosa, int Origine, int Importati)> Righe { get; } = [];
    public List<(string Codice, decimal SulRicambio, decimal PerUbicazione)> Differenze { get; } = [];
    public List<(string Tabella, int Righe, string Motivo)> NonImportati { get; } = [];
    public List<string> Avvisi { get; } = [];
    public decimal ValoreMagazzino { get; set; }

    public void Aggiungi(string cosa, int origine, int importati) => Righe.Add((cosa, origine, importati));

    public string ComeMarkdown(string origine)
    {
        var it = CultureInfo.GetCultureInfo("it-IT");
        var sb = new StringBuilder();
        sb.AppendLine("# Report importazione da Gestione Manutenzione").AppendLine();
        sb.AppendLine($"Origine: `{origine}`  ").AppendLine($"Eseguita: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", it)}").AppendLine();
        sb.AppendLine("## Conteggi").AppendLine().AppendLine("| Dato | Nel vecchio | Importati | Esito |").AppendLine("| --- | ---: | ---: | --- |");
        foreach (var (c, o, i) in Righe) sb.AppendLine($"| {c} | {o} | {i} | {(o == i ? "ok" : "DA CONTROLLARE")} |");
        sb.AppendLine().AppendLine($"Valore del magazzino importato (giacenza x costo medio): {ValoreMagazzino.ToString("C", it)}").AppendLine();
        sb.AppendLine("## Giacenze che non tornavano nel vecchio programma").AppendLine();
        if (Differenze.Count == 0) sb.AppendLine("Nessuna: la quantita' sul ricambio coincide con la somma per ubicazione.");
        else
        {
            sb.AppendLine("Vale la giacenza per ubicazione; da verificare con una conta.").AppendLine();
            sb.AppendLine("| Ricambio | Quantita' sul ricambio | Somma per ubicazione (importata) |").AppendLine("| --- | ---: | ---: |");
            foreach (var (c, a, b) in Differenze) sb.AppendLine($"| {c} | {a:0.##} | {b:0.##} |");
        }
        sb.AppendLine().AppendLine("## Non importati in questo passo").AppendLine().AppendLine("| Tabella | Righe | Motivo |").AppendLine("| --- | ---: | --- |");
        foreach (var (t, n, m) in NonImportati) sb.AppendLine($"| {t} | {n} | {m} |");
        if (Avvisi.Count > 0)
        {
            sb.AppendLine().AppendLine("## Avvisi").AppendLine();
            foreach (var a in Avvisi) sb.AppendLine($"- {a}");
        }
        return sb.ToString();
    }
}
