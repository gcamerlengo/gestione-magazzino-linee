using Microsoft.EntityFrameworkCore;

namespace Magazzino.Infrastructure.Servizi;

public class Numeratore(MagazzinoDbContext db)
{
    public async Task<(string Numero, int Anno, int Progressivo)> ProssimoAsync(string serie, DateTime data)
    {
        var anno = data.Year;
        var c = db.Contatori.Local.FirstOrDefault(x => x.Serie == serie && x.Anno == anno)
                ?? await db.Contatori.FirstOrDefaultAsync(x => x.Serie == serie && x.Anno == anno);
        if (c is null)
        {
            c = new Contatore { Serie = serie, Anno = anno };
            db.Contatori.Add(c);
        }
        c.Ultimo++;
        return ($"{serie}-{anno}-{c.Ultimo:D6}", anno, c.Ultimo);
    }
}
