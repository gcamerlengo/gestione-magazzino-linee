using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Magazzino.Infrastructure.Migrazioni
{
    /// <inheritdoc />
    public partial class Iniziale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contatori",
                columns: table => new
                {
                    Serie = table.Column<string>(type: "text", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Ultimo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contatori", x => new { x.Serie, x.Anno });
                });

            migrationBuilder.CreateTable(
                name: "Fornitori",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RagioneSociale = table.Column<string>(type: "text", nullable: false),
                    PartitaIva = table.Column<string>(type: "text", nullable: false),
                    Referente = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<string>(type: "text", nullable: false),
                    EmailOrdini = table.Column<string>(type: "text", nullable: false),
                    CondizioniPagamento = table.Column<string>(type: "text", nullable: false),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    IdOrigine = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fornitori", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Linee",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codice = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Reparto = table.Column<string>(type: "text", nullable: false),
                    Attiva = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Linee", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RichiesteAcquisto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Richiedente = table.Column<string>(type: "text", nullable: false),
                    Stato = table.Column<int>(type: "integer", nullable: false),
                    DaRiordinoAutomatico = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RichiesteAcquisto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ubicazioni",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codice = table.Column<string>(type: "text", nullable: false),
                    Magazzino = table.Column<string>(type: "text", nullable: false),
                    Scaffale = table.Column<string>(type: "text", nullable: false),
                    Ripiano = table.Column<string>(type: "text", nullable: false),
                    Cella = table.Column<string>(type: "text", nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: false),
                    Attiva = table.Column<bool>(type: "boolean", nullable: false),
                    IdOrigine = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ubicazioni", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Utenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    NomeVisualizzato = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Ruolo = table.Column<int>(type: "integer", nullable: false),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utenti", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrdiniFornitore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    FornitoreId = table.Column<int>(type: "integer", nullable: false),
                    DataOrdine = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataConsegnaPrevista = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Stato = table.Column<int>(type: "integer", nullable: false),
                    ApprovatoDa = table.Column<string>(type: "text", nullable: false),
                    ApprovatoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FilePdf = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    IdOrigine = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdiniFornitore", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdiniFornitore_Fornitori_FornitoreId",
                        column: x => x.FornitoreId,
                        principalTable: "Fornitori",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ricevimenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    FornitoreId = table.Column<int>(type: "integer", nullable: false),
                    NumeroDdt = table.Column<string>(type: "text", nullable: false),
                    DataDdt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RicevutoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RicevutoDa = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ricevimenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ricevimenti_Fornitori_FornitoreId",
                        column: x => x.FornitoreId,
                        principalTable: "Fornitori",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Macchine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LineaId = table.Column<int>(type: "integer", nullable: false),
                    CodiceAsset = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Costruttore = table.Column<string>(type: "text", nullable: false),
                    Modello = table.Column<string>(type: "text", nullable: false),
                    Matricola = table.Column<string>(type: "text", nullable: false),
                    AnnoCostruzione = table.Column<int>(type: "integer", nullable: true),
                    Criticita = table.Column<int>(type: "integer", nullable: false),
                    DpiRichiesti = table.Column<string>(type: "text", nullable: false),
                    Attiva = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    IdOrigine = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Macchine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Macchine_Linee_LineaId",
                        column: x => x.LineaId,
                        principalTable: "Linee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ricambi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codice = table.Column<string>(type: "text", nullable: false),
                    CodiceCostruttore = table.Column<string>(type: "text", nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    UnitaMisura = table.Column<string>(type: "text", nullable: false),
                    CostoMedio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ScortaMinima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PuntoRiordino = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantitaRiordino = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LeadTimeGiorni = table.Column<int>(type: "integer", nullable: false),
                    Critico = table.Column<bool>(type: "boolean", nullable: false),
                    AlimentareNsfH1 = table.Column<bool>(type: "boolean", nullable: false),
                    FornitorePreferitoId = table.Column<int>(type: "integer", nullable: true),
                    UbicazionePredefinitaId = table.Column<int>(type: "integer", nullable: true),
                    FotoPath = table.Column<string>(type: "text", nullable: false),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    IdOrigine = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ricambi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ricambi_Fornitori_FornitorePreferitoId",
                        column: x => x.FornitorePreferitoId,
                        principalTable: "Fornitori",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ricambi_Ubicazioni_UbicazionePredefinitaId",
                        column: x => x.UbicazionePredefinitaId,
                        principalTable: "Ubicazioni",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Interventi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codice = table.Column<string>(type: "text", nullable: false),
                    LineaId = table.Column<int>(type: "integer", nullable: false),
                    MacchinaId = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<int>(type: "integer", nullable: false),
                    Stato = table.Column<int>(type: "integer", nullable: false),
                    Inizio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fine = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OreLavoro = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Tecnico = table.Column<string>(type: "text", nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: false),
                    PezzoSmontato = table.Column<int>(type: "integer", nullable: false),
                    CostoManodopera = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CostoEsterni = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FotoPath = table.Column<string>(type: "text", nullable: false),
                    IdOrigine = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interventi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interventi_Linee_LineaId",
                        column: x => x.LineaId,
                        principalTable: "Linee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Interventi_Macchine_MacchinaId",
                        column: x => x.MacchinaId,
                        principalTable: "Macchine",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Giacenze",
                columns: table => new
                {
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    UbicazioneId = table.Column<int>(type: "integer", nullable: false),
                    Quantita = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UltimoMovimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Versione = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Giacenze", x => new { x.RicambioId, x.UbicazioneId });
                    table.ForeignKey(
                        name: "FK_Giacenze_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Giacenze_Ubicazioni_UbicazioneId",
                        column: x => x.UbicazioneId,
                        principalTable: "Ubicazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movimenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Progressivo = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Utente = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    UbicazioneDaId = table.Column<int>(type: "integer", nullable: true),
                    UbicazioneAId = table.Column<int>(type: "integer", nullable: true),
                    Quantita = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TipoDocumento = table.Column<string>(type: "text", nullable: false),
                    DocumentoId = table.Column<int>(type: "integer", nullable: true),
                    LineaId = table.Column<int>(type: "integer", nullable: true),
                    InterventoId = table.Column<int>(type: "integer", nullable: true),
                    StornaMovimentoId = table.Column<int>(type: "integer", nullable: true),
                    Stornato = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movimenti_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RicambiFornitori",
                columns: table => new
                {
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    FornitoreId = table.Column<int>(type: "integer", nullable: false),
                    CodiceFornitore = table.Column<string>(type: "text", nullable: false),
                    Prezzo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LottoMinimo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LeadTimeGiorni = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicambiFornitori", x => new { x.RicambioId, x.FornitoreId });
                    table.ForeignKey(
                        name: "FK_RicambiFornitori_Fornitori_FornitoreId",
                        column: x => x.FornitoreId,
                        principalTable: "Fornitori",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RicambiFornitori_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RicambiMacchine",
                columns: table => new
                {
                    MacchinaId = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    QuantitaConsigliata = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicambiMacchine", x => new { x.MacchinaId, x.RicambioId });
                    table.ForeignKey(
                        name: "FK_RicambiMacchine_Macchine_MacchinaId",
                        column: x => x.MacchinaId,
                        principalTable: "Macchine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RicambiMacchine_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigaRichiestaAcquisto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RichiestaAcquistoId = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    Quantita = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    LineaDestinazioneId = table.Column<int>(type: "integer", nullable: true),
                    RigaOrdineFornitoreId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigaRichiestaAcquisto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigaRichiestaAcquisto_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RigaRichiestaAcquisto_RichiesteAcquisto_RichiestaAcquistoId",
                        column: x => x.RichiestaAcquistoId,
                        principalTable: "RichiesteAcquisto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigheOrdineFornitore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdineFornitoreId = table.Column<int>(type: "integer", nullable: false),
                    NumeroRiga = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: true),
                    CodiceFornitore = table.Column<string>(type: "text", nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: false),
                    UnitaMisura = table.Column<string>(type: "text", nullable: false),
                    Quantita = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantitaRicevuta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PrezzoUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheOrdineFornitore", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigheOrdineFornitore_OrdiniFornitore_OrdineFornitoreId",
                        column: x => x.OrdineFornitoreId,
                        principalTable: "OrdiniFornitore",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RigheOrdineFornitore_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BuoniPrelievo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    LineaId = table.Column<int>(type: "integer", nullable: true),
                    MacchinaId = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<int>(type: "integer", nullable: false),
                    MotivoScaricoGenerico = table.Column<string>(type: "text", nullable: false),
                    Stato = table.Column<int>(type: "integer", nullable: false),
                    Tecnico = table.Column<string>(type: "text", nullable: false),
                    InterventoId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuoniPrelievo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuoniPrelievo_Interventi_InterventoId",
                        column: x => x.InterventoId,
                        principalTable: "Interventi",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BuoniPrelievo_Linee_LineaId",
                        column: x => x.LineaId,
                        principalTable: "Linee",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BuoniPrelievo_Macchine_MacchinaId",
                        column: x => x.MacchinaId,
                        principalTable: "Macchine",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RicambioUsato",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InterventoId = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    Quantita = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MovimentoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicambioUsato", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RicambioUsato_Interventi_InterventoId",
                        column: x => x.InterventoId,
                        principalTable: "Interventi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RicambioUsato_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigaRicevimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RicevimentoId = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    RigaOrdineFornitoreId = table.Column<int>(type: "integer", nullable: true),
                    UbicazioneId = table.Column<int>(type: "integer", nullable: false),
                    Quantita = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PrezzoUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigaRicevimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigaRicevimento_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RigaRicevimento_Ricevimenti_RicevimentoId",
                        column: x => x.RicevimentoId,
                        principalTable: "Ricevimenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RigaRicevimento_RigheOrdineFornitore_RigaOrdineFornitoreId",
                        column: x => x.RigaOrdineFornitoreId,
                        principalTable: "RigheOrdineFornitore",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RigaBuonoPrelievo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuonoPrelievoId = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    UbicazioneId = table.Column<int>(type: "integer", nullable: false),
                    Quantita = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantitaResa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigaBuonoPrelievo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigaBuonoPrelievo_BuoniPrelievo_BuonoPrelievoId",
                        column: x => x.BuonoPrelievoId,
                        principalTable: "BuoniPrelievo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RigaBuonoPrelievo_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuoniPrelievo_InterventoId",
                table: "BuoniPrelievo",
                column: "InterventoId");

            migrationBuilder.CreateIndex(
                name: "IX_BuoniPrelievo_LineaId",
                table: "BuoniPrelievo",
                column: "LineaId");

            migrationBuilder.CreateIndex(
                name: "IX_BuoniPrelievo_MacchinaId",
                table: "BuoniPrelievo",
                column: "MacchinaId");

            migrationBuilder.CreateIndex(
                name: "IX_BuoniPrelievo_Numero",
                table: "BuoniPrelievo",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fornitori_RagioneSociale",
                table: "Fornitori",
                column: "RagioneSociale",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Giacenze_UbicazioneId",
                table: "Giacenze",
                column: "UbicazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_Interventi_Codice",
                table: "Interventi",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interventi_LineaId",
                table: "Interventi",
                column: "LineaId");

            migrationBuilder.CreateIndex(
                name: "IX_Interventi_MacchinaId",
                table: "Interventi",
                column: "MacchinaId");

            migrationBuilder.CreateIndex(
                name: "IX_Linee_Codice",
                table: "Linee",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Macchine_LineaId_CodiceAsset",
                table: "Macchine",
                columns: new[] { "LineaId", "CodiceAsset" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movimenti_Numero",
                table: "Movimenti",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movimenti_RicambioId_Data",
                table: "Movimenti",
                columns: new[] { "RicambioId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniFornitore_FornitoreId",
                table: "OrdiniFornitore",
                column: "FornitoreId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniFornitore_Numero",
                table: "OrdiniFornitore",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ricambi_Codice",
                table: "Ricambi",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ricambi_FornitorePreferitoId",
                table: "Ricambi",
                column: "FornitorePreferitoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ricambi_UbicazionePredefinitaId",
                table: "Ricambi",
                column: "UbicazionePredefinitaId");

            migrationBuilder.CreateIndex(
                name: "IX_RicambiFornitori_FornitoreId",
                table: "RicambiFornitori",
                column: "FornitoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RicambiMacchine_RicambioId",
                table: "RicambiMacchine",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_RicambioUsato_InterventoId",
                table: "RicambioUsato",
                column: "InterventoId");

            migrationBuilder.CreateIndex(
                name: "IX_RicambioUsato_RicambioId",
                table: "RicambioUsato",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ricevimenti_FornitoreId",
                table: "Ricevimenti",
                column: "FornitoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Ricevimenti_Numero",
                table: "Ricevimenti",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteAcquisto_Numero",
                table: "RichiesteAcquisto",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RigaBuonoPrelievo_BuonoPrelievoId",
                table: "RigaBuonoPrelievo",
                column: "BuonoPrelievoId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaBuonoPrelievo_RicambioId",
                table: "RigaBuonoPrelievo",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaRicevimento_RicambioId",
                table: "RigaRicevimento",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaRicevimento_RicevimentoId",
                table: "RigaRicevimento",
                column: "RicevimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaRicevimento_RigaOrdineFornitoreId",
                table: "RigaRicevimento",
                column: "RigaOrdineFornitoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaRichiestaAcquisto_RicambioId",
                table: "RigaRichiestaAcquisto",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaRichiestaAcquisto_RichiestaAcquistoId",
                table: "RigaRichiestaAcquisto",
                column: "RichiestaAcquistoId");

            migrationBuilder.CreateIndex(
                name: "IX_RigheOrdineFornitore_OrdineFornitoreId",
                table: "RigheOrdineFornitore",
                column: "OrdineFornitoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RigheOrdineFornitore_RicambioId",
                table: "RigheOrdineFornitore",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ubicazioni_Codice",
                table: "Ubicazioni",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_Username",
                table: "Utenti",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contatori");

            migrationBuilder.DropTable(
                name: "Giacenze");

            migrationBuilder.DropTable(
                name: "Movimenti");

            migrationBuilder.DropTable(
                name: "RicambiFornitori");

            migrationBuilder.DropTable(
                name: "RicambiMacchine");

            migrationBuilder.DropTable(
                name: "RicambioUsato");

            migrationBuilder.DropTable(
                name: "RigaBuonoPrelievo");

            migrationBuilder.DropTable(
                name: "RigaRicevimento");

            migrationBuilder.DropTable(
                name: "RigaRichiestaAcquisto");

            migrationBuilder.DropTable(
                name: "Utenti");

            migrationBuilder.DropTable(
                name: "BuoniPrelievo");

            migrationBuilder.DropTable(
                name: "Ricevimenti");

            migrationBuilder.DropTable(
                name: "RigheOrdineFornitore");

            migrationBuilder.DropTable(
                name: "RichiesteAcquisto");

            migrationBuilder.DropTable(
                name: "Interventi");

            migrationBuilder.DropTable(
                name: "OrdiniFornitore");

            migrationBuilder.DropTable(
                name: "Ricambi");

            migrationBuilder.DropTable(
                name: "Macchine");

            migrationBuilder.DropTable(
                name: "Fornitori");

            migrationBuilder.DropTable(
                name: "Ubicazioni");

            migrationBuilder.DropTable(
                name: "Linee");
        }
    }
}
