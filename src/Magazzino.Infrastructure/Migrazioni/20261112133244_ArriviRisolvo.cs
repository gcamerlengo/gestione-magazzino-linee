using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Magazzino.Infrastructure.Migrazioni
{
    /// <inheritdoc />
    public partial class ArriviRisolvo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbbinamentiMateriale",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestoNormalizzato = table.Column<string>(type: "text", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbbinamentiMateriale", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbbinamentiMateriale_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArriviEsterni",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sorgente = table.Column<string>(type: "text", nullable: false),
                    IdEsterno = table.Column<string>(type: "text", nullable: false),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Materiale = table.Column<string>(type: "text", nullable: false),
                    QuantitaTesto = table.Column<string>(type: "text", nullable: false),
                    Luogo = table.Column<string>(type: "text", nullable: false),
                    FornitoreTesto = table.Column<string>(type: "text", nullable: false),
                    RiferimentoOrdine = table.Column<string>(type: "text", nullable: false),
                    RicevutoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Stato = table.Column<int>(type: "integer", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: true),
                    Quantita = table.Column<decimal>(type: "numeric", nullable: true),
                    UbicazioneId = table.Column<int>(type: "integer", nullable: true),
                    ComeRiconosciuto = table.Column<string>(type: "text", nullable: false),
                    Certo = table.Column<bool>(type: "boolean", nullable: false),
                    RicevimentoId = table.Column<int>(type: "integer", nullable: true),
                    CaricatoInAutomatico = table.Column<bool>(type: "boolean", nullable: false),
                    GestitoDa = table.Column<string>(type: "text", nullable: false),
                    GestitoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArriviEsterni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArriviEsterni_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ArriviEsterni_Ricevimenti_RicevimentoId",
                        column: x => x.RicevimentoId,
                        principalTable: "Ricevimenti",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ArriviEsterni_Ubicazioni_UbicazioneId",
                        column: x => x.UbicazioneId,
                        principalTable: "Ubicazioni",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Impostazioni",
                columns: table => new
                {
                    Chiave = table.Column<string>(type: "text", nullable: false),
                    Valore = table.Column<string>(type: "text", nullable: false),
                    AggiornataIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Impostazioni", x => x.Chiave);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbbinamentiMateriale_RicambioId",
                table: "AbbinamentiMateriale",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_AbbinamentiMateriale_TestoNormalizzato",
                table: "AbbinamentiMateriale",
                column: "TestoNormalizzato",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArriviEsterni_RicambioId",
                table: "ArriviEsterni",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_ArriviEsterni_RicevimentoId",
                table: "ArriviEsterni",
                column: "RicevimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArriviEsterni_Sorgente_IdEsterno",
                table: "ArriviEsterni",
                columns: new[] { "Sorgente", "IdEsterno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArriviEsterni_Stato",
                table: "ArriviEsterni",
                column: "Stato");

            migrationBuilder.CreateIndex(
                name: "IX_ArriviEsterni_UbicazioneId",
                table: "ArriviEsterni",
                column: "UbicazioneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbbinamentiMateriale");

            migrationBuilder.DropTable(
                name: "ArriviEsterni");

            migrationBuilder.DropTable(
                name: "Impostazioni");
        }
    }
}
