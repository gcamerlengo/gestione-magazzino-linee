using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Magazzino.Infrastructure.Migrazioni
{
    /// <inheritdoc />
    public partial class Scatole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Scatole",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codice = table.Column<string>(type: "text", nullable: false),
                    RicambioId = table.Column<int>(type: "integer", nullable: false),
                    UbicazioneId = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    Attiva = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scatole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scatole_Ricambi_RicambioId",
                        column: x => x.RicambioId,
                        principalTable: "Ricambi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Scatole_Ubicazioni_UbicazioneId",
                        column: x => x.UbicazioneId,
                        principalTable: "Ubicazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scatole_Codice",
                table: "Scatole",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scatole_RicambioId",
                table: "Scatole",
                column: "RicambioId");

            migrationBuilder.CreateIndex(
                name: "IX_Scatole_UbicazioneId",
                table: "Scatole",
                column: "UbicazioneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Scatole");
        }
    }
}
