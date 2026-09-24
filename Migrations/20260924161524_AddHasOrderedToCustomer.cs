using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinhPhat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHasOrderedToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasOrdered",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_customers_HasOrdered",
                table: "customers",
                column: "HasOrdered");

            migrationBuilder.Sql("UPDATE customers SET \"HasOrdered\" = true WHERE \"Id\" IN (SELECT DISTINCT \"CustomerId\" FROM orders);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customers_HasOrdered",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "HasOrdered",
                table: "customers");
        }
    }
}
