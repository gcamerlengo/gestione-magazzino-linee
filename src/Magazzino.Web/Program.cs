using Magazzino.Infrastructure;
using Magazzino.Web;
using Magazzino.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;

// Installato su Windows gira come servizio: i file del programma stanno accanto all'eseguibile.
var comeServizio = WindowsServiceHelpers.IsWindowsService();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = comeServizio ? AppContext.BaseDirectory : null,
});
builder.Host.UseWindowsService(o => o.ServiceName = "MagazzinoRicambi");

// Impostazioni locali dell'installazione (database, indirizzo, porta): fuori dalla cartella del
// programma, cosi' un aggiornamento non le sovrascrive.
var cartellaDati = builder.Configuration["Magazzino:CartellaDati"]
    ?? (OperatingSystem.IsWindows() && comeServizio
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MagazzinoRicambi")
        : null);
if (cartellaDati is not null)
{
    builder.Configuration.AddJsonFile(Path.Combine(cartellaDati, "impostazioni.json"), optional: true, reloadOnChange: false);
    // Chiavi che cifrano il cookie di accesso: salvate su disco, cosi' dopo un riavvio nessuno deve rientrare.
    builder.Services.AddDataProtection()
        .SetApplicationName("MagazzinoRicambi")
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(cartellaDati, "chiavi")));
}

builder.Services.AddMagazzino(builder.Configuration.GetConnectionString("Magazzino")
    ?? throw new InvalidOperationException("Manca la stringa di connessione 'Magazzino'."));
builder.Services.AddSingleton<Magazzino.Web.IndirizziQr>();
builder.Services.AddAccessi();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Aggiorna la struttura del database all'avvio (prima installazione e aggiornamenti del programma).
if (app.Configuration.GetValue("Magazzino:AggiornaDatabaseAllAvvio", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<MagazzinoDbContext>().Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Nessun redirect a HTTPS: in rete aziendale il programma risponde in HTTP sulla porta configurata.
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAccessi();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
