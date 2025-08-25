using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace apiCrud.Migrations
{
    /// <inheritdoc />
    public partial class ActualizacionEntidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "nombre",
                table: "Users",
                newName: "Nombre");

            migrationBuilder.RenameColumn(
                name: "apellido",
                table: "Users",
                newName: "Apellido");

            migrationBuilder.CreateTable(
                name: "RawEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    DispositivoId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoEvento = table.Column<string>(type: "TEXT", nullable: false),
                    MarcaDeTiempo = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Payload_json = table.Column<string>(type: "TEXT", nullable: false),
                    Lote_ingesta = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawEvents", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawEvents");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "Users",
                newName: "nombre");

            migrationBuilder.RenameColumn(
                name: "Apellido",
                table: "Users",
                newName: "apellido");
        }
    }
}
