using System.Security.Claims;
using Magazzino.Domain.Anagrafiche;
using Magazzino.Infrastructure;
using Magazzino.Infrastructure.Servizi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Web;

/// <summary>Regole di accesso: chi puo' fare cosa.</summary>
public static class Permessi
{
    /// <summary>Pagine dell'ufficio (ordini, giacenze, anagrafiche).</summary>
    public const string Ufficio = "Ufficio";
    /// <summary>Approvare gli ordini sopra soglia.</summary>
    public const string Approvare = "Approvare";
    /// <summary>Gestire utenti e ruoli.</summary>
    public const string Amministrare = "Amministrare";

    public const string ClaimId = "magazzino:id";
    public const string ClaimUsername = "magazzino:username";
    public const string ClaimImpronta = "magazzino:impronta";

    public static readonly string[] RuoliUfficio = [nameof(RuoloUtente.Ufficio), nameof(RuoloUtente.Responsabile), nameof(RuoloUtente.Admin)];

    public static string Descrizione(RuoloUtente r) => r switch
    {
        RuoloUtente.Tecnico => "Tecnico (solo app dal telefono)",
        RuoloUtente.Ufficio => "Ufficio (ordini, giacenze, anagrafiche)",
        RuoloUtente.Responsabile => "Responsabile (come ufficio, e approva gli ordini)",
        RuoloUtente.Admin => "Amministratore (tutto, e gestisce gli utenti)",
        _ => r.ToString()
    };

    public static IServiceCollection AddAccessi(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "magazzino.accesso";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.LoginPath = "/accesso";
                o.LogoutPath = "/esci";
                o.AccessDeniedPath = "/accesso/negato";
                o.ReturnUrlParameter = "ritorno";
                o.ExpireTimeSpan = TimeSpan.FromDays(30); // i tecnici restano collegati sul telefono
                o.SlidingExpiration = true;
                o.Events.OnValidatePrincipal = ControllaUtenteAncoraValido;
            });
        services.AddAuthorization(o =>
        {
            o.AddPolicy(Ufficio, p => p.RequireRole(RuoliUfficio));
            o.AddPolicy(Approvare, p => p.RequireRole(nameof(RuoloUtente.Responsabile), nameof(RuoloUtente.Admin)));
            o.AddPolicy(Amministrare, p => p.RequireRole(nameof(RuoloUtente.Admin)));
        });
        services.AddCascadingAuthenticationState();
        services.AddScoped<UtenteCorrente>();
        return services;
    }

    public static ClaimsPrincipal Principal(Utente u) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.Name, u.NomeVisualizzato),
        new Claim(ClaimTypes.Role, u.Ruolo.ToString()),
        new Claim(ClaimId, u.Id.ToString()),
        new Claim(ClaimUsername, u.Username),
        new Claim(ClaimImpronta, UtentiService.Impronta(u)),
    ], CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>
    /// A ogni richiesta: se l'utente e' stato disattivato, ha cambiato ruolo o password,
    /// la sessione aperta (anche sul telefono) non vale piu' e si torna al login.
    /// </summary>
    private static async Task ControllaUtenteAncoraValido(CookieValidatePrincipalContext ctx)
    {
        var id = int.TryParse(ctx.Principal?.FindFirstValue(ClaimId), out var v) ? v : 0;
        var impronta = ctx.Principal?.FindFirstValue(ClaimImpronta);
        await using var db = await ctx.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<MagazzinoDbContext>>().CreateDbContextAsync();
        var u = await db.Utenti.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u is null || !u.Attivo || UtentiService.Impronta(u) != impronta)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        else if (u.NomeVisualizzato != ctx.Principal!.Identity!.Name)
        {
            ctx.ReplacePrincipal(Principal(u)); // nome corretto dall'amministratore
            ctx.ShouldRenew = true;
        }
    }

    public static void MapAccessi(this WebApplication app)
    {
        app.MapPost("/esci", async (HttpContext c, [Microsoft.AspNetCore.Mvc.FromForm] string? ritorno) =>
        {
            await c.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/accesso");
        }).AllowAnonymous();
    }

    /// <summary>Solo indirizzi interni all'applicazione, per non rimandare l'utente su un altro sito dopo il login.</summary>
    public static string RitornoSicuro(string? ritorno, RuoloUtente ruolo)
    {
        var predefinito = ruolo == RuoloUtente.Tecnico ? "/t" : "/";
        if (string.IsNullOrEmpty(ritorno) || !ritorno.StartsWith('/') || ritorno.StartsWith("//") || ritorno.StartsWith("/\\")) return predefinito;
        if (ritorno.StartsWith("/accesso", StringComparison.OrdinalIgnoreCase)) return predefinito;
        return ritorno;
    }
}

/// <summary>Chi sta usando il programma: nome registrato su movimenti, ordini e interventi.</summary>
public class UtenteCorrente(AuthenticationStateProvider auth)
{
    public async Task<ClaimsPrincipal> PrincipalAsync() => (await auth.GetAuthenticationStateAsync()).User;

    public async Task<string> NomeAsync() => (await PrincipalAsync()).Identity?.Name ?? "?";

    public async Task<int> IdAsync() => int.TryParse((await PrincipalAsync()).FindFirstValue(Permessi.ClaimId), out var v) ? v : 0;

    public async Task<bool> PuoAsync(string ruoloOPermesso)
    {
        var p = await PrincipalAsync();
        return ruoloOPermesso switch
        {
            Permessi.Ufficio => Permessi.RuoliUfficio.Any(p.IsInRole),
            Permessi.Approvare => p.IsInRole(nameof(RuoloUtente.Responsabile)) || p.IsInRole(nameof(RuoloUtente.Admin)),
            Permessi.Amministrare => p.IsInRole(nameof(RuoloUtente.Admin)),
            _ => p.IsInRole(ruoloOPermesso)
        };
    }
}
