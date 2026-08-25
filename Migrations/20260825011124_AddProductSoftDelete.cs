using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinhPhat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_products_IsDeleted",
                table: "products",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_IsDeleted",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "products");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "products");
        }
    }
}
