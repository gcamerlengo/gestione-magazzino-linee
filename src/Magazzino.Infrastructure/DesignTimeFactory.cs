using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Magazzino.Infrastructure;

/// <summary>Usato solo da "dotnet ef" per generare le migrazioni.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<MagazzinoDbContext>
{
    public MagazzinoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MagazzinoDbContext>()
            .UseNpgsql("Host=localhost;Database=magazzino;Username=magazzino;Password=cambiami").Options);
}
