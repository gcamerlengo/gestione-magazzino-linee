using Microsoft.Extensions.DependencyInjection;

namespace Magazzino.Infrastructure.Servizi;

/// <summary>
/// Esegue ogni operazione in uno scope nuovo (DbContext pulito). Nelle pagine Blazor Server il circuito
/// vive a lungo: un DbContext condiviso accumulerebbe entita' vecchie e conflitti tra azioni.
/// </summary>
public class Operazioni(IServiceScopeFactory scopes)
{
    public async Task<T> EseguiAsync<T>(Func<IServiceProvider, Task<T>> op)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await op(scope.ServiceProvider);
    }

    public async Task EseguiAsync(Func<IServiceProvider, Task> op)
    {
        await using var scope = scopes.CreateAsyncScope();
        await op(scope.ServiceProvider);
    }
}
