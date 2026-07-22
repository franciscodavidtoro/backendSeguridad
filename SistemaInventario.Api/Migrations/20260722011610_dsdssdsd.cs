using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaInventario.Api.Migrations
{
    /// <inheritdoc />
    public partial class dsdssdsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Elementos");

            migrationBuilder.DropColumn(
                name: "Precio",
                table: "Elementos");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "Elementos",
                newName: "NombreBien");

            migrationBuilder.RenameColumn(
                name: "Descripcion",
                table: "Elementos",
                newName: "Ubicacion");

            migrationBuilder.RenameColumn(
                name: "CodigoBarras",
                table: "Elementos",
                newName: "CodigoBien");

            migrationBuilder.AddColumn<string>(
                name: "MarcaRazaOtros",
                table: "Elementos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modelo",
                table: "Elementos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Serie",
                table: "Elementos",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarcaRazaOtros",
                table: "Elementos");

            migrationBuilder.DropColumn(
                name: "Modelo",
                table: "Elementos");

            migrationBuilder.DropColumn(
                name: "Serie",
                table: "Elementos");

            migrationBuilder.RenameColumn(
                name: "Ubicacion",
                table: "Elementos",
                newName: "Descripcion");

            migrationBuilder.RenameColumn(
                name: "NombreBien",
                table: "Elementos",
                newName: "Nombre");

            migrationBuilder.RenameColumn(
                name: "CodigoBien",
                table: "Elementos",
                newName: "CodigoBarras");

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Elementos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Precio",
                table: "Elementos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
