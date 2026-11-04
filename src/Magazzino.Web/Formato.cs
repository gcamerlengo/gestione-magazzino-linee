using Magazzino.Domain.Giacenze;
using System.Globalization;

namespace Magazzino.Web;

public static class Formato
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static readonly TimeZoneInfo Roma = Trova();

    public static string Q(decimal q) => q.ToString("#,0.##", It);
    public static string Euro(decimal v) => v.ToString("C", It);
    public static string Data(DateTime? d) => d is null ? "" : Locale(d.Value).ToString("dd/MM/yyyy", It);
    public static string DataOra(DateTime d) => Locale(d).ToString("dd/MM/yyyy HH:mm", It);
    public static DateTime Locale(DateTime d) => d.Kind == DateTimeKind.Utc ? TimeZoneInfo.ConvertTimeFromUtc(d, Roma) : d;

    public static string Tipo(TipoMovimento t) => t switch
    {
        TipoMovimento.CaricoFornitore => "Carico fornitore",
        TipoMovimento.ScaricoIntervento => "Scarico per intervento",
        TipoMovimento.ScaricoGenerico => "Scarico generico",
        TipoMovimento.RettificaInventario => "Rettifica inventario",
        _ => t.ToString()
    };

    private static TimeZoneInfo Trova()
    {
        foreach (var id in new[] { "Europe/Rome", "W. Europe Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        return TimeZoneInfo.Local;
    }
}
