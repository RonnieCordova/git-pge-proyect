using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace apiCrud.Migrations
{
    /// <inheritdoc />
    public partial class CambioEntidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawEvents");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.CreateTable(
                name: "BiometricoData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: true),
                    Apellido = table.Column<string>(type: "TEXT", nullable: true),
                    Hora = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Detalle = table.Column<string>(type: "TEXT", nullable: true),
                    EsEntrada = table.Column<bool>(type: "INTEGER", nullable: false),
                    EsSalida = table.Column<bool>(type: "INTEGER", nullable: false),
                    EsSalidaAlmuerzo = table.Column<bool>(type: "INTEGER", nullable: false),
                    EsLlegadaAlmuerzo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiometricoData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeatData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: true),
                    Apellido = table.Column<string>(type: "TEXT", nullable: true),
                    HoraEntrada = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    HoraSalidaAlmuerzo = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    HoraRegresoAlmuerzo = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    HoraSalida = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Detalle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatData", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiometricoData");

            migrationBuilder.DropTable(
                name: "SeatData");

            migrationBuilder.CreateTable(
                name: "RawEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DispositivoId = table.Column<string>(type: "TEXT", nullable: true),
                    Lote_ingesta = table.Column<string>(type: "TEXT", nullable: true),
                    MarcaDeTiempo = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    TipoEvento = table.Column<string>(type: "TEXT", nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Apellido = table.Column<string>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }
    }
}
