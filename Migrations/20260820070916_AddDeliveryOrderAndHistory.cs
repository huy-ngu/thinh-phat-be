using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinhPhat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryOrderAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    GhtkId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "delivery_order_status_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_order_status_histories");

            migrationBuilder.DropTable(
                name: "delivery_orders");
        }
    }
}
