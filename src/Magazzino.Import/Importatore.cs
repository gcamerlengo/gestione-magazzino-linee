using System.Globalization;
using Magazzino.Domain.Anagrafiche;
using Magazzino.Domain.Carico;
using Magazzino.Domain.Giacenze;
using Magazzino.Domain.Scarico;
using Magazzino.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Import;

/// <summary>
/// Importa i dati da manutenzione.db (Gestione Manutenzione) nel nuovo database.
/// Il database di origine e' aperto in sola lettura; la destinazione deve essere vuota.
/// Tutto avviene in una transazione: o si importa tutto o niente.
/// </summary>
public class Importatore(SqliteConnection origine, MagazzinoDbContext db)
{
    private static readonly TimeZoneInfo Roma = TrovaFusoRoma();
    private readonly Dictionary<int, Linea> _linee = [];
    private readonly Dictionary<int, Macchina> _macchine = [];
    private readonly Dictionary<int, Fornitore> _fornitori = [];
    private readonly Dictionary<int, Ubicazione> _ubicazioni = [];
    private readonly Dictionary<int, Ricambio> _ricambi = [];
    private readonly Dictionary<int, decimal> _qtaSulRicambio = [];

    public const string UtenteImport = "import";

    /// <param name="soloProva">Esegue tutto e annulla la transazione: nessun dato resta scritto.</param>
    public async Task<ReportImport> EseguiAsync(bool soloProva = false)
    {
        if (await db.Linee.AnyAsync() || await db.Ricambi.AnyAsync() || await db.Movimenti.AnyAsync())
            throw new InvalidOperationException("Il database di destinazione non e' vuoto: l'importazione si fa una sola volta su un database nuovo.");

        var r = new ReportImport();
        await using var tx = await db.Database.BeginTransactionAsync();

        ImportaLinee(r);
        ImportaMacchine(r);
        ImportaFornitori(r);
        ImportaUbicazioni(r);
        await db.SaveChangesAsync();

        ImportaRicambi(r);
        await db.SaveChangesAsync();
        ImportaRicambiMacchine(r);

        await ImportaGiacenzeAsync(r);
        await db.SaveChangesAsync();

        await ImportaOrdiniAsync(r);
        await ImportaStoricoInterventiAsync(r);
        await db.SaveChangesAsync();

        r.NonImportati.Add(("plan_items", Conta("plan_items"), "piani preventivi: arrivano con il modulo scadenzario"));
        r.NonImportati.Add(("work_orders aperti", Conta("work_orders", "status in (0,1)"), "scadenze aperte: si rigenerano dal piano"));
        r.NonImportati.Add(("stock_movements", Conta("stock_movements"), "storico movimenti: sostituito dal movimento di apertura"));
        r.NonImportati.Add(("imported_tickets", Conta("imported_tickets"), "ticket: arrivano con l'integrazione ticket"));
        r.NonImportati.Add(("users", Conta("users"), "utenti: si ricreano con i nuovi ruoli"));

        if (soloProva) await tx.RollbackAsync(); else await tx.CommitAsync();
        return r;
    }

    private void ImportaLinee(ReportImport r)
    {
        foreach (var x in Leggi("select id, name, notes, active from lines"))
        {
            var l = new Linea { Codice = S(x, "name"), Nome = S(x, "name"), Note = S(x, "notes"), Attiva = B(x, "active") };
            _linee[I(x, "id")] = l;
            db.Linee.Add(l);
        }
        r.Aggiungi("Linee", Conta("lines"), _linee.Count);
    }

    private void ImportaMacchine(ReportImport r)
    {
        foreach (var x in Leggi("select * from machines"))
        {
            if (!_linee.TryGetValue(I(x, "line_id"), out var linea)) { r.Avvisi.Add($"Macchina {S(x, "asset_code")}: linea {I(x, "line_id")} inesistente, saltata."); continue; }
            var m = new Macchina
            {
                Linea = linea, CodiceAsset = S(x, "asset_code"), Nome = S(x, "name"), Tipo = S(x, "equipment_type"),
                Costruttore = S(x, "manufacturer"), Modello = S(x, "model_name"),
                Matricola = string.Join(" / ", new[] { S(x, "serial_internal"), S(x, "serial_inail") }.Where(s => s != "")),
                AnnoCostruzione = int.TryParse(S(x, "year_built"), out var anno) ? anno : null,
                Criticita = S(x, "criticality").ToLowerInvariant() switch { "bassa" => 1, "media" => 2, "alta" => 3, _ => 0 },
                DpiRichiesti = S(x, "required_ppe"), Attiva = B(x, "active"), Note = S(x, "notes"), IdOrigine = I(x, "id")
            };
            _macchine[I(x, "id")] = m;
            db.Macchine.Add(m);
        }
        r.Aggiungi("Macchine", Conta("machines"), _macchine.Count);
    }

    private void ImportaFornitori(ReportImport r)
    {
        foreach (var x in Leggi("select * from suppliers"))
        {
            var f = new Fornitore
            {
                RagioneSociale = S(x, "name"), Referente = S(x, "contact"), Telefono = S(x, "phone"),
                EmailOrdini = S(x, "email"), Note = S(x, "notes"), Attivo = B(x, "active"), IdOrigine = I(x, "id")
            };
            _fornitori[I(x, "id")] = f;
            db.Fornitori.Add(f);
        }
        // Ricambi con fornitore scritto solo come testo: si crea il fornitore mancante.
        foreach (var nome in Leggi("select distinct trim(supplier) s from spare_parts where supplier_id is null and trim(supplier) <> ''").Select(x => S(x, "s")))
        {
            if (_fornitori.Values.Any(f => f.RagioneSociale.Equals(nome, StringComparison.OrdinalIgnoreCase))) continue;
            var f = new Fornitore { RagioneSociale = nome, Note = "Creato dall'importazione (nome sul ricambio)" };
            _fornitori[-_fornitori.Count - 1] = f;
            db.Fornitori.Add(f);
            r.Avvisi.Add($"Fornitore \"{nome}\" creato dal nome scritto sul ricambio: controllare i dati.");
        }
        r.Aggiungi("Fornitori", Conta("suppliers"), _fornitori.Keys.Count(k => k > 0));
        if (_fornitori.Keys.Any(k => k < 0))
            r.Aggiungi("Fornitori creati dal nome sul ricambio", _fornitori.Keys.Count(k => k < 0), _fornitori.Keys.Count(k => k < 0));
    }

    private void ImportaUbicazioni(ReportImport r)
    {
        foreach (var x in Leggi("select * from stock_locations"))
        {
            var u = new Ubicazione
            {
                Codice = S(x, "code"), Magazzino = S(x, "warehouse"), Scaffale = S(x, "shelf"), Ripiano = S(x, "level"),
                Cella = S(x, "bin"), Descrizione = S(x, "description"), Attiva = B(x, "active"), IdOrigine = I(x, "id")
            };
            _ubicazioni[I(x, "id")] = u;
            db.Ubicazioni.Add(u);
        }
        r.Aggiungi("Ubicazioni", Conta("stock_locations"), _ubicazioni.Count);
    }

    private void ImportaRicambi(ReportImport r)
    {
        foreach (var x in Leggi("select * from spare_parts"))
        {
            var id = I(x, "id");
            Fornitore? forn = null;
            if (x["supplier_id"] is long sid) _fornitori.TryGetValue((int)sid, out forn);
            forn ??= _fornitori.Values.FirstOrDefault(f => f.RagioneSociale.Equals(S(x, "supplier").Trim(), StringComparison.OrdinalIgnoreCase));
            Ubicazione? ubi = null;
            if (x["default_location_id"] is long lid) _ubicazioni.TryGetValue((int)lid, out ubi);

            var rc = new Ricambio
            {
                Codice = S(x, "code"), CodiceCostruttore = S(x, "manufacturer_code"), Descrizione = S(x, "name"),
                Categoria = Enum.IsDefined(typeof(CategoriaRicambio), I(x, "category")) ? (CategoriaRicambio)I(x, "category") : CategoriaRicambio.Altro,
                UnitaMisura = S(x, "unit_of_measure") is "" ? "pz" : S(x, "unit_of_measure"),
                CostoMedio = D(x, "unit_cost"), ScortaMinima = D(x, "minimum_stock"), PuntoRiordino = D(x, "reorder_threshold"),
                QuantitaRiordino = D(x, "reorder_quantity"), LeadTimeGiorni = I(x, "lead_time_days"), Critico = B(x, "critical"),
                AlimentareNsfH1 = B(x, "food_grade"), FornitorePreferito = forn, UbicazionePredefinita = ubi,
                FotoPath = S(x, "photo_path"), Attivo = B(x, "active"), Note = S(x, "notes"), IdOrigine = id
            };
            _ricambi[id] = rc;
            _qtaSulRicambio[id] = D(x, "quantity_on_hand");
            db.Ricambi.Add(rc);
        }
        r.Aggiungi("Ricambi", Conta("spare_parts"), _ricambi.Count);
    }

    private void ImportaRicambiMacchine(ReportImport r)
    {
        var n = 0;
        foreach (var x in Leggi("select * from machine_spare_parts"))
        {
            if (!_macchine.TryGetValue(I(x, "machine_id"), out var m) || !_ricambi.TryGetValue(I(x, "spare_part_id"), out var rc)) continue;
            db.RicambiMacchine.Add(new RicambioMacchina { Macchina = m, Ricambio = rc, QuantitaConsigliata = D(x, "recommended_quantity"), Note = S(x, "notes") });
            n++;
        }
        r.Aggiungi("Ricambi per macchina", Conta("machine_spare_parts"), n);
    }

    /// <summary>Una giacenza per ubicazione diventa un movimento di apertura. Vale stock_levels; le differenze con quantity_on_hand vanno nel report.</summary>
    private async Task ImportaGiacenzeAsync(ReportImport r)
    {
        var numeratore = new Infrastructure.Servizi.Numeratore(db);
        var perRicambio = new Dictionary<int, decimal>();
        var n = 0;
        foreach (var x in Leggi("select * from stock_levels where quantity <> 0"))
        {
            var pid = I(x, "spare_part_id");
            if (!_ricambi.TryGetValue(pid, out var rc) || !_ubicazioni.TryGetValue(I(x, "location_id"), out var u)) continue;
            var q = D(x, "quantity");
            if (q < 0) { r.Avvisi.Add($"{rc.Codice} in {u.Codice}: giacenza negativa ({q}) portata a zero."); continue; }
            var data = Data(x, "last_movement_at") ?? DateTime.UtcNow;
            var (numero, anno, prog) = await numeratore.ProssimoAsync("MOV", data);
            db.Movimenti.Add(new Movimento
            {
                Numero = numero, Anno = anno, Progressivo = prog, Data = data, Utente = UtenteImport, Tipo = TipoMovimento.Apertura,
                RicambioId = rc.Id, UbicazioneAId = u.Id, Quantita = q, CostoUnitario = rc.CostoMedio,
                TipoDocumento = "Import", Note = "Giacenza iniziale da Gestione Manutenzione"
            });
            db.Giacenze.Add(new Giacenza { RicambioId = rc.Id, UbicazioneId = u.Id, Quantita = q, UltimoMovimento = data });
            perRicambio[pid] = perRicambio.GetValueOrDefault(pid) + q;
            r.ValoreMagazzino += q * rc.CostoMedio;
            n++;
        }
        r.Aggiungi("Giacenze (movimenti di apertura)", Conta("stock_levels", "quantity <> 0"), n);

        foreach (var (pid, sulRicambio) in _qtaSulRicambio)
        {
            var perUbicazione = perRicambio.GetValueOrDefault(pid);
            if (perUbicazione != sulRicambio)
                r.Differenze.Add((_ricambi[pid].Codice, sulRicambio, perUbicazione));
        }
    }

    private async Task ImportaOrdiniAsync(ReportImport r)
    {
        var n = 0;
        var ordini = Leggi("select * from purchase_orders").ToList();
        var righe = Leggi("select * from purchase_order_lines order by purchase_order_id, line_number").ToList();
        foreach (var x in ordini)
        {
            var oid = I(x, "id");
            var forn = x["supplier_id"] is long sid && _fornitori.TryGetValue((int)sid, out var f) ? f
                : _fornitori.Values.FirstOrDefault(v => v.RagioneSociale.Equals(S(x, "supplier_name"), StringComparison.OrdinalIgnoreCase));
            if (forn is null) { r.Avvisi.Add($"Ordine {S(x, "number")}: fornitore \"{S(x, "supplier_name")}\" non trovato, saltato."); continue; }
            var o = new OrdineFornitore
            {
                Numero = S(x, "number"), Fornitore = forn, DataOrdine = Data(x, "order_date") ?? Data(x, "created_at") ?? DateTime.UtcNow,
                DataConsegnaPrevista = Data(x, "expected_date"), FilePdf = S(x, "source_file"), Note = S(x, "notes"), IdOrigine = oid
            };
            foreach (var y in righe.Where(y => I(y, "purchase_order_id") == oid))
            {
                Ricambio? rc = y["spare_part_id"] is long pid && _ricambi.TryGetValue((int)pid, out var t) ? t : null;
                o.Righe.Add(new RigaOrdineFornitore
                {
                    NumeroRiga = I(y, "line_number"), Ricambio = rc, CodiceFornitore = S(y, "code"), Descrizione = S(y, "description"),
                    UnitaMisura = S(y, "unit_of_measure") is "" ? "pz" : S(y, "unit_of_measure"),
                    Quantita = D(y, "quantity_ordered"), QuantitaRicevuta = D(y, "quantity_received"), PrezzoUnitario = D(y, "unit_cost")
                });
            }
            var tuttoRicevuto = o.Righe.Count > 0 && o.Righe.All(l => l.QuantitaRicevuta >= l.Quantita);
            var qualcosaRicevuto = o.Righe.Any(l => l.QuantitaRicevuta > 0);
            o.Stato = B(x, "closed") ? StatoOrdineFornitore.Chiuso
                : tuttoRicevuto ? StatoOrdineFornitore.Ricevuto
                : qualcosaRicevuto ? StatoOrdineFornitore.RicevutoInParte
                : StatoOrdineFornitore.Inviato;
            db.OrdiniFornitore.Add(o);
            n++;
        }
        r.Aggiungi("Ordini fornitore", ordini.Count, n);
        await Task.CompletedTask;
    }

    /// <summary>Gli ordini di lavoro completati diventano interventi storici sulla linea della macchina (senza movimenti).</summary>
    private async Task ImportaStoricoInterventiAsync(ReportImport r)
    {
        var parti = Leggi("select * from work_order_parts").ToList();
        var n = 0;
        foreach (var x in Leggi("select * from work_orders where status = 2"))
        {
            if (!_macchine.TryGetValue(I(x, "machine_id"), out var m)) continue;
            var wid = I(x, "id");
            var fine = Data(x, "executed_at") ?? Data(x, "updated_at") ?? DateTime.UtcNow;
            var i = new Intervento
            {
                Codice = S(x, "code"), Linea = m.Linea, Macchina = m,
                Motivo = I(x, "kind") switch { 0 => MotivoIntervento.Preventiva, 1 => MotivoIntervento.Guasto, _ => MotivoIntervento.Altro },
                Stato = StatoIntervento.Completato, Inizio = fine, Fine = fine, OreLavoro = D(x, "duration_hours"),
                Tecnico = S(x, "technician_name"),
                Descrizione = string.Join("\n", new[] { S(x, "summary"), S(x, "anomalies_found"), S(x, "parts_used"), S(x, "notes") }.Where(s => s != "")),
                CostoManodopera = D(x, "labor_cost"), CostoEsterni = D(x, "external_cost"), FotoPath = S(x, "photo_path"), IdOrigine = wid
            };
            foreach (var p in parti.Where(p => I(p, "work_order_id") == wid))
                if (_ricambi.TryGetValue(I(p, "spare_part_id"), out var rc))
                    i.RicambiUsati.Add(new RicambioUsato { Ricambio = rc, Quantita = D(p, "quantity"), CostoUnitario = D(p, "unit_cost_at_use") });
            db.Interventi.Add(i);
            n++;
        }
        r.Aggiungi("Interventi completati (storico)", Conta("work_orders", "status = 2"), n);
        r.Aggiungi("Ricambi usati negli interventi", Conta("work_order_parts"), db.ChangeTracker.Entries<RicambioUsato>().Count());
        await Task.CompletedTask;
    }

    // ---- lettura origine ----

    private IEnumerable<Dictionary<string, object?>> Leggi(string sql)
    {
        using var cmd = origine.CreateCommand();
        cmd.CommandText = sql;
        using var rd = cmd.ExecuteReader();
        var res = new List<Dictionary<string, object?>>();
        while (rd.Read())
        {
            var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var k = 0; k < rd.FieldCount; k++) d[rd.GetName(k)] = rd.IsDBNull(k) ? null : rd.GetValue(k);
            res.Add(d);
        }
        return res;
    }

    private int Conta(string tabella, string? dove = null)
    {
        using var cmd = origine.CreateCommand();
        cmd.CommandText = "select count(*) from sqlite_master where type='table' and name=$t";
        cmd.Parameters.AddWithValue("$t", tabella);
        if ((long)cmd.ExecuteScalar()! == 0) return 0;
        cmd.Parameters.Clear();
        cmd.CommandText = $"select count(*) from \"{tabella}\"" + (dove is null ? "" : $" where {dove}");
        return (int)(long)cmd.ExecuteScalar()!;
    }

    private static string S(Dictionary<string, object?> x, string k) => x.TryGetValue(k, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture)!.Trim() : "";
    private static int I(Dictionary<string, object?> x, string k) => x.TryGetValue(k, out var v) && v is not null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : 0;
    private static bool B(Dictionary<string, object?> x, string k) => I(x, k) != 0;

    private static decimal D(Dictionary<string, object?> x, string k)
    {
        if (!x.TryGetValue(k, out var v) || v is null) return 0;
        if (v is string s) return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
        return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
    }

    /// <summary>Le date del vecchio programma sono ora locale italiana senza fuso: si convertono in UTC.</summary>
    private static DateTime? Data(Dictionary<string, object?> x, string k)
    {
        var s = S(x, k);
        if (s == "" || !DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return null;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), Roma);
    }

    private static TimeZoneInfo TrovaFusoRoma()
    {
        foreach (var id in new[] { "Europe/Rome", "W. Europe Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        return TimeZoneInfo.Utc;
    }
}
