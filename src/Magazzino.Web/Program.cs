using Magazzino.Infrastructure;
using Magazzino.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMagazzino(builder.Configuration.GetConnectionString("Magazzino")
    ?? throw new InvalidOperationException("Manca la stringa di connessione 'Magazzino'."));
builder.Services.AddSingleton<Magazzino.Web.IndirizziQr>();
builder.Services.AddScoped<Magazzino.Web.TecnicoCorrente>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
