using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinhPhat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatedTimesToDeliveryOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstimatedDeliverTime",
                table: "delivery_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatedPickTime",
                table: "delivery_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedDeliverTime",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "EstimatedPickTime",
                table: "delivery_orders");
        }
    }
}
