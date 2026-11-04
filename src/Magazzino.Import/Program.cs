using Magazzino.Import;
using Magazzino.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// Uso: Magazzino.Import <percorso manutenzione.db> "<connessione PostgreSQL>" [report.md] [--prova]
// --prova: esegue tutto e annulla alla fine (nessun dato scritto), utile per vedere il report.
if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: Magazzino.Import <manutenzione.db> \"Host=...;Database=...;Username=...;Password=...\" [report.md] [--prova]");
    return 2;
}
var prova = args.Contains("--prova");
var pos = args.Where(a => !a.StartsWith("--")).ToArray();
var sorgente = pos[0];
var connessione = pos[1];
var reportPath = pos.Length > 2 ? pos[2] : "report-importazione.md";

if (!File.Exists(sorgente)) { Console.Error.WriteLine($"File non trovato: {sorgente}"); return 2; }

await using var src = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sorgente, Mode = SqliteOpenMode.ReadOnly }.ToString());
await src.OpenAsync();
await using var db = new MagazzinoDbContext(new DbContextOptionsBuilder<MagazzinoDbContext>().UseNpgsql(connessione).Options);
await db.Database.MigrateAsync();

try
{
    var report = await new Importatore(src, db).EseguiAsync(prova);
    Console.WriteLine(prova ? "Prova completata: nessun dato scritto." : "Importazione completata.");
    var md = report.ComeMarkdown(sorgente);
    await File.WriteAllTextAsync(reportPath, md);
    Console.WriteLine(md);
    Console.WriteLine($"Report salvato in {Path.GetFullPath(reportPath)}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Importazione annullata: {ex.Message}");
    return 1;
}
