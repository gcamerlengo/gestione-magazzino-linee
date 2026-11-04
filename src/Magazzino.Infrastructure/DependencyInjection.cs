using Magazzino.Infrastructure.Servizi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Magazzino.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMagazzino(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MagazzinoDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<Numeratore>();
        services.AddScoped<GiacenzeService>();
        services.AddScoped<CaricoService>();
        services.AddScoped<ScaricoService>();
        services.AddScoped<RiordinoService>();
        return services;
    }
}
