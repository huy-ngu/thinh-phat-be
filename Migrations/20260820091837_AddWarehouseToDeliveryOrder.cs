using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinhPhat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseToDeliveryOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "delivery_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_WarehouseId",
                table: "delivery_orders",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_orders_warehouses_WarehouseId",
                table: "delivery_orders",
                column: "WarehouseId",
                principalTable: "warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_orders_warehouses_WarehouseId",
                table: "delivery_orders");

            migrationBuilder.DropIndex(
                name: "IX_delivery_orders_WarehouseId",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "delivery_orders");
        }
    }
}
