using Magazzino.Domain;
using Magazzino.Domain.Anagrafiche;
using Magazzino.Domain.Carico;
using Magazzino.Domain.Giacenze;
using Magazzino.Domain.Scarico;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure;

public class MagazzinoDbContext(DbContextOptions<MagazzinoDbContext> options) : DbContext(options)
{
    public DbSet<Linea> Linee => Set<Linea>();
    public DbSet<Macchina> Macchine => Set<Macchina>();
    public DbSet<Ricambio> Ricambi => Set<Ricambio>();
    public DbSet<RicambioMacchina> RicambiMacchine => Set<RicambioMacchina>();
    public DbSet<Fornitore> Fornitori => Set<Fornitore>();
    public DbSet<RicambioFornitore> RicambiFornitori => Set<RicambioFornitore>();
    public DbSet<Ubicazione> Ubicazioni => Set<Ubicazione>();
    public DbSet<Utente> Utenti => Set<Utente>();

    public DbSet<RichiestaAcquisto> RichiesteAcquisto => Set<RichiestaAcquisto>();
    public DbSet<OrdineFornitore> OrdiniFornitore => Set<OrdineFornitore>();
    public DbSet<RigaOrdineFornitore> RigheOrdineFornitore => Set<RigaOrdineFornitore>();
    public DbSet<Ricevimento> Ricevimenti => Set<Ricevimento>();

    public DbSet<BuonoPrelievo> BuoniPrelievo => Set<BuonoPrelievo>();
    public DbSet<Intervento> Interventi => Set<Intervento>();

    public DbSet<Movimento> Movimenti => Set<Movimento>();
    public DbSet<Giacenza> Giacenze => Set<Giacenza>();
    public DbSet<Contatore> Contatori => Set<Contatore>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Linea>().HasIndex(x => x.Codice).IsUnique();
        b.Entity<Macchina>().HasIndex(x => new { x.LineaId, x.CodiceAsset }).IsUnique();
        b.Entity<Ricambio>().HasIndex(x => x.Codice).IsUnique();
        b.Entity<Ubicazione>().HasIndex(x => x.Codice).IsUnique();
        b.Entity<Fornitore>().HasIndex(x => x.RagioneSociale).IsUnique();
        b.Entity<Utente>().HasIndex(x => x.Username).IsUnique();

        b.Entity<RicambioMacchina>().HasKey(x => new { x.MacchinaId, x.RicambioId });
        b.Entity<RicambioFornitore>().HasKey(x => new { x.RicambioId, x.FornitoreId });

        b.Entity<OrdineFornitore>().HasIndex(x => x.Numero).IsUnique();
        b.Entity<OrdineFornitore>().Ignore(x => x.Totale);
        b.Entity<RigaOrdineFornitore>().Ignore(x => x.Residuo);
        b.Entity<RigaOrdineFornitore>().HasOne(x => x.Ordine).WithMany(x => x.Righe).HasForeignKey(x => x.OrdineFornitoreId);
        b.Entity<RichiestaAcquisto>().HasIndex(x => x.Numero).IsUnique();
        b.Entity<Ricevimento>().HasIndex(x => x.Numero).IsUnique();

        b.Entity<BuonoPrelievo>().HasIndex(x => x.Numero).IsUnique();
        b.Entity<Intervento>().HasIndex(x => x.Codice).IsUnique();
        b.Entity<Intervento>().Ignore(x => x.CostoRicambi);

        b.Entity<Movimento>().HasIndex(x => x.Numero).IsUnique();
        b.Entity<Movimento>().HasIndex(x => new { x.RicambioId, x.Data });
        b.Entity<Giacenza>().HasKey(x => new { x.RicambioId, x.UbicazioneId });
        b.Entity<Giacenza>().Property(x => x.Versione).IsConcurrencyToken();
        b.Entity<Contatore>().HasKey(x => new { x.Serie, x.Anno });

        foreach (var p in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal)))
            p.SetPrecision(18); 
        foreach (var p in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal)))
            p.SetScale(4);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var e in ChangeTracker.Entries<Entity>().Where(e => e.State == EntityState.Modified))
            e.Entity.UpdatedAt = DateTime.UtcNow;
        foreach (var e in ChangeTracker.Entries<Giacenza>().Where(e => e.State == EntityState.Modified))
            e.Entity.Versione++;
        return base.SaveChangesAsync(ct);
    }
}

/// <summary>Numerazione progressiva per serie e anno (es. MOV-2026-000123).</summary>
public class Contatore
{
    public string Serie { get; set; } = "";
    public int Anno { get; set; }
    public int Ultimo { get; set; }
}
