using QRCoder;

namespace Magazzino.Web;

public static class Qr
{
    /// <summary>SVG del QR, da inserire direttamente nella pagina (nessun file immagine).</summary>
    public static string Svg(string testo)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(testo, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(4, "#000000", "#ffffff", drawQuietZones: true, sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}

/// <summary>Indirizzo stampato nel QR: la fotocamera del telefono apre direttamente il prelievo.</summary>
public class IndirizziQr(IConfiguration config)
{
    public string PerScatola(string codice, string baseUri)
    {
        var b = config["Magazzino:IndirizzoPubblico"];
        if (string.IsNullOrWhiteSpace(b)) b = baseUri;
        return $"{b.TrimEnd('/')}/t/s/{Uri.EscapeDataString(codice)}";
    }
}
