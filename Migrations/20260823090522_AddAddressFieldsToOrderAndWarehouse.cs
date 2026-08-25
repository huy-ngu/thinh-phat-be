using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinhPhat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressFieldsToOrderAndWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "District",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "District",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "orders");
        }
    }
}
