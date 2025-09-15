using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace apiCrud.Migrations
{
    /// <inheritdoc />
    public partial class ActArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "SeatData",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Area",
                table: "SeatData");
        }
    }
}
