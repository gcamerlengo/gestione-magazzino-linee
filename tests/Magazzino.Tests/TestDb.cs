using Magazzino.Domain.Anagrafiche;
using Magazzino.Infrastructure;
using Magazzino.Infrastructure.Servizi;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Tests;

/// <summary>Database SQLite in memoria con dati minimi: 1 linea, 1 macchina, 1 fornitore, 2 ubicazioni, 1 ricambio.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    public MagazzinoDbContext Db { get; }
    public GiacenzeService Giacenze { get; }
    public CaricoService Carico { get; }
    public ScaricoService Scarico { get; }
    public RiordinoService Riordino { get; }
    public OrdiniService Ordini { get; }
    public ScatoleService Scatole { get; }
    public UtentiService Utenti { get; }
    public ArriviService Arrivi { get; }
    public Linea Linea { get; }
    public Macchina Macchina { get; }
    public Fornitore Fornitore { get; }
    public Ubicazione U1 { get; }
    public Ubicazione U2 { get; }
    public Ricambio Cuscinetto { get; }

    public TestDb()
    {
        _conn.Open();
        Db = new MagazzinoDbContext(new DbContextOptionsBuilder<MagazzinoDbContext>().UseSqlite(_conn).Options);
        Db.Database.EnsureCreated();
        var num = new Numeratore(Db);
        Giacenze = new GiacenzeService(Db, num);
        Carico = new CaricoService(Db, Giacenze, num);
        Scarico = new ScaricoService(Db, Giacenze, num);
        Riordino = new RiordinoService(Db, num);
        Ordini = new OrdiniService(Db, num);
        Scatole = new ScatoleService(Db, num);
        Utenti = new UtentiService(Db);
        Arrivi = new ArriviService(Db, Carico);

        Linea = new Linea { Codice = "L1", Nome = "LINEA 1" };
        Macchina = new Macchina { Linea = Linea, CodiceAsset = "L1-RIEMP", Nome = "Riempitrice" };
        Fornitore = new Fornitore { RagioneSociale = "SKF" };
        U1 = new Ubicazione { Codice = "A-01-1" };
        U2 = new Ubicazione { Codice = "A-01-2" };
        Cuscinetto = new Ricambio { Codice = "6204-2RS", Descrizione = "Cuscinetto 6204", PuntoRiordino = 4, QuantitaRiordino = 10, FornitorePreferito = Fornitore };
        Db.AddRange(Linea, Macchina, Fornitore, U1, U2, Cuscinetto);
        Db.SaveChanges();
    }

    public void Dispose()
    {
        Db.Dispose();
        _conn.Dispose();
    }
}
