using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaInventario.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMfa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MfaSecretKey",
                table: "Usuarios",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaSecretKey",
                table: "Usuarios");
        }
    }
}
