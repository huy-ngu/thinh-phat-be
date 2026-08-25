using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinhPhat.Api.Migrations
{
    /// <inheritdoc />
    public partial class MergeDeliveryOrderIntoOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_products_ProductId",
                table: "orders");

            migrationBuilder.DropTable(
                name: "delivery_order_status_histories");

            migrationBuilder.DropTable(
                name: "delivery_orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerId_CreatedAtUtc",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_ProductId_CreatedAtUtc",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "orders");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "EstimatedDeliverTime",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatedPickTime",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhtkId",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_status_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_status_histories_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatedAtUtc",
                table: "orders",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId",
                table: "orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_GhtkId",
                table: "orders",
                column: "GhtkId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_CreatedAtUtc",
                table: "orders",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_WarehouseId",
                table: "orders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ProductId",
                table: "order_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_histories_OrderId_CreatedAtUtc",
                table: "order_status_histories",
                columns: new[] { "OrderId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_order_status_histories_Status",
                table: "order_status_histories",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_warehouses_WarehouseId",
                table: "orders",
                column: "WarehouseId",
                principalTable: "warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_warehouses_WarehouseId",
                table: "orders");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "order_status_histories");

            migrationBuilder.DropIndex(
                name: "IX_orders_CreatedAtUtc",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_GhtkId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_Status_CreatedAtUtc",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_WarehouseId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "EstimatedDeliverTime",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "EstimatedPickTime",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "GhtkId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "orders");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "delivery_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    EstimatedDeliverTime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstimatedPickTime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GhtkId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ShippingFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_orders_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_delivery_orders_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "delivery_order_status_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_order_status_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_order_status_histories_delivery_orders_DeliveryOrd~",
                        column: x => x.DeliveryOrderId,
                        principalTable: "delivery_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId_CreatedAtUtc",
                table: "orders",
                columns: new[] { "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_ProductId_CreatedAtUtc",
                table: "orders",
                columns: new[] { "ProductId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_status_histories_DeliveryOrderId_CreatedAtUtc",
                table: "delivery_order_status_histories",
                columns: new[] { "DeliveryOrderId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_status_histories_Status",
                table: "delivery_order_status_histories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_CreatedAtUtc",
                table: "delivery_orders",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_GhtkId",
                table: "delivery_orders",
                column: "GhtkId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_OrderId",
                table: "delivery_orders",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_Status",
                table: "delivery_orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_Status_CreatedAtUtc",
                table: "delivery_orders",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_WarehouseId",
                table: "delivery_orders",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_products_ProductId",
                table: "orders",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
