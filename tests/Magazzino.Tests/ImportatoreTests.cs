using Magazzino.Domain.Carico;
using Magazzino.Domain.Giacenze;
using Magazzino.Domain.Scarico;
using Magazzino.Import;
using Magazzino.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Magazzino.Tests;

public sealed class ImportatoreTests : IDisposable
{
    private readonly SqliteConnection _vecchio = new("DataSource=:memory:");
    private readonly SqliteConnection _nuovoConn = new("DataSource=:memory:");
    private readonly MagazzinoDbContext _db;

    // Schema ridotto di Gestione Manutenzione, con le colonne lette dall'importatore.
    private const string SchemaVecchio = """
        create table lines (id integer primary key, name text, notes text default '', active integer default 1);
        create table machines (id integer primary key, line_id integer, asset_code text, name text, equipment_type text default '',
          manufacturer text default '', model_name text default '', serial_internal text default '', serial_inail text default '',
          year_built text, criticality text default 'Media', required_ppe text default '', active integer default 1, notes text default '');
        create table suppliers (id integer primary key, name text, contact text default '', phone text default '', email text default '', notes text default '', active integer default 1);
        create table stock_locations (id integer primary key, code text, warehouse text default '', shelf text default '', level text default '', bin text default '', description text default '', active integer default 1);
        create table spare_parts (id integer primary key, code text, name text, unit_of_measure text default 'pz', quantity_on_hand real default 0,
          reorder_threshold real default 0, unit_cost text default '0', supplier text default '', food_grade integer default 0, notes text default '',
          active integer default 1, manufacturer_code text default '', category integer default 0, supplier_id integer, minimum_stock real default 0,
          reorder_quantity real default 0, lead_time_days integer default 0, critical integer default 0, default_location_id integer, photo_path text default '');
        create table machine_spare_parts (machine_id integer, spare_part_id integer, recommended_quantity real, notes text default '');
        create table stock_levels (spare_part_id integer, location_id integer, quantity real, last_movement_at text);
        create table stock_movements (id integer primary key);
        create table purchase_orders (id integer primary key, number text, supplier_id integer, supplier_name text, order_date text, expected_date text,
          closed integer default 0, source_file text default '', notes text default '', created_at text);
        create table purchase_order_lines (id integer primary key, purchase_order_id integer, line_number integer, spare_part_id integer, code text,
          description text, unit_of_measure text, quantity_ordered real, quantity_received real, unit_cost text);
        create table work_orders (id integer primary key, code text, machine_id integer, kind integer, status integer, executed_at text, updated_at text,
          duration_hours real default 0, technician_name text default '', summary text default '', anomalies_found text default '', parts_used text default '',
          notes text default '', labor_cost text default '0', external_cost text default '0', photo_path text default '');
        create table work_order_parts (id integer primary key, work_order_id integer, spare_part_id integer, quantity real, unit_cost_at_use text);
        create table plan_items (id integer primary key);

        insert into lines values (1, 'LINEA 1', '', 1), (2, 'LINEA 4A', '', 1);
        insert into machines (id, line_id, asset_code, name, criticality) values (10, 1, 'LINEA1-RIEMPITRICE', 'Riempitrice', 'Alta'), (11, 2, 'LINEA4A-MONOBLOCCO', 'Monoblocco', 'Media');
        insert into suppliers (id, name) values (1, 'SKF Italia');
        insert into stock_locations (id, code) values (1, 'GEN'), (2, 'A-01-1');
        insert into spare_parts (id, code, name, quantity_on_hand, reorder_threshold, unit_cost, supplier, supplier_id, default_location_id)
          values (2, 'CUS-6204', 'Cuscinetto 6204', 23, 6, '4.1478', 'SKF Italia', 1, 2),
                 (3, 'OR-32X3', 'O-ring 32x3', 150, 0, '0.3', 'Ditta Nuova', null, null);
        insert into machine_spare_parts values (10, 2, 2, '');
        insert into stock_levels values (2, 2, 23, '2026-10-14 11:21:25'), (3, 1, 200, '2026-10-14 11:21:25');
        insert into purchase_orders (id, number, supplier_id, supplier_name, order_date) values (1, 'OA-2026/0457', 1, 'SKF Italia', '2026-10-05 00:00:00');
        insert into purchase_order_lines values (1, 1, 1, 2, 'CUS-6204', 'Cuscinetto', 'pz', 10, 10, '4.05'), (2, 1, 2, 3, 'OR-32X3', 'O-ring', 'pz', 100, 40, '0.3');
        insert into work_orders (id, code, machine_id, kind, status, executed_at, duration_hours, technician_name, summary)
          values (1, 'MP-1', 10, 0, 2, '2026-09-16 00:00:00', 1.5, 'ffuccio', 'Settimanale'), (2, 'MP-2', 10, 0, 0, null, 0, '', '');
        insert into work_order_parts values (1, 1, 2, 2, '4.0');
        """;

    public ImportatoreTests()
    {
        _vecchio.Open();
        using (var cmd = _vecchio.CreateCommand()) { cmd.CommandText = SchemaVecchio; cmd.ExecuteNonQuery(); }
        _nuovoConn.Open();
        _db = new MagazzinoDbContext(new DbContextOptionsBuilder<MagazzinoDbContext>().UseSqlite(_nuovoConn).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Importa_anagrafiche_giacenze_ordini_e_storico()
    {
        var r = await new Importatore(_vecchio, _db).EseguiAsync();
        _db.ChangeTracker.Clear();

        Assert.Equal(2, await _db.Linee.CountAsync());
        var riemp = await _db.Macchine.Include(m => m.Linea).SingleAsync(m => m.CodiceAsset == "LINEA1-RIEMPITRICE");
        Assert.Equal("LINEA 1", riemp.Linea!.Nome);
        Assert.Equal(3, riemp.Criticita);

        var cus = await _db.Ricambi.Include(x => x.FornitorePreferito).Include(x => x.UbicazionePredefinita).SingleAsync(x => x.Codice == "CUS-6204");
        Assert.Equal(4.1478m, cus.CostoMedio);
        Assert.Equal("SKF Italia", cus.FornitorePreferito!.RagioneSociale);
        Assert.Equal("A-01-1", cus.UbicazionePredefinita!.Codice);
        Assert.Equal(1, await _db.RicambiMacchine.CountAsync());

        // Fornitore scritto solo come testo sul ricambio: viene creato e segnalato.
        var or = await _db.Ricambi.Include(x => x.FornitorePreferito).SingleAsync(x => x.Codice == "OR-32X3");
        Assert.Equal("Ditta Nuova", or.FornitorePreferito!.RagioneSociale);
        Assert.Contains(r.Avvisi, a => a.Contains("Ditta Nuova"));

        // Giacenze: un movimento di apertura per ubicazione, e la differenza col vecchio quantity_on_hand e' nel report.
        Assert.Equal(2, await _db.Movimenti.CountAsync(m => m.Tipo == TipoMovimento.Apertura));
        Assert.Equal(200, (await _db.Giacenze.SingleAsync(g => g.RicambioId == or.Id)).Quantita);
        Assert.Contains(r.Differenze, d => d.Codice == "OR-32X3" && d.SulRicambio == 150 && d.PerUbicazione == 200);
        Assert.DoesNotContain(r.Differenze, d => d.Codice == "CUS-6204");

        // Ordine ricevuto solo in parte: stato ricalcolato.
        var o = await _db.OrdiniFornitore.Include(x => x.Righe).SingleAsync();
        Assert.Equal(StatoOrdineFornitore.RicevutoInParte, o.Stato);
        Assert.Equal(60, o.Righe.Single(x => x.NumeroRiga == 2).Residuo);

        // Solo gli ordini di lavoro completati diventano interventi, sulla linea della macchina.
        var i = await _db.Interventi.Include(x => x.RicambiUsati).SingleAsync();
        Assert.Equal(riemp.LineaId, i.LineaId);
        Assert.Equal(StatoIntervento.Completato, i.Stato);
        Assert.Equal(8m, i.CostoRicambi);
        Assert.Equal(new DateTime(2026, 9, 15, 22, 0, 0, DateTimeKind.Utc), i.Fine); // mezzanotte a Roma (ora legale) = 22:00 UTC

        Assert.Contains(r.NonImportati, n => n.Tabella == "work_orders aperti" && n.Righe == 1);
        Assert.All(r.Righe.Where(x => x.Cosa != "Fornitori creati dal nome sul ricambio"), x => Assert.Equal(x.Origine, x.Importati));
    }

    [Fact]
    public async Task Rifiuta_database_non_vuoto()
    {
        await new Importatore(_vecchio, _db).EseguiAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new Importatore(_vecchio, _db).EseguiAsync());
    }

    [Fact]
    public async Task Prova_non_scrive_nulla()
    {
        var r = await new Importatore(_vecchio, _db).EseguiAsync(soloProva: true);
        _db.ChangeTracker.Clear();
        Assert.Equal(2, r.Righe.Single(x => x.Cosa == "Linee").Importati);
        Assert.Equal(0, await _db.Linee.CountAsync());
        Assert.Equal(0, await _db.Movimenti.CountAsync());
    }

    [Fact]
    public async Task Report_markdown_segnala_differenze()
    {
        var md = (await new Importatore(_vecchio, _db).EseguiAsync()).ComeMarkdown("test.db");
        Assert.Contains("| OR-32X3 | 150 | 200 |", md);
        Assert.Contains("| Linee | 2 | 2 | ok |", md);
    }

    public void Dispose()
    {
        _db.Dispose();
        _nuovoConn.Dispose();
        _vecchio.Dispose();
    }
}
