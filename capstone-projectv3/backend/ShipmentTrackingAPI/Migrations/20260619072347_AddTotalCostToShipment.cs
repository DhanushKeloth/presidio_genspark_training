using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipmentTrackingAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalCostToShipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropColumn(
            //     name: "updated_at",
            //     table: "saved_addresses");

            migrationBuilder.AddColumn<decimal>(
                name: "total_cost",
                table: "shipments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "total_cost",
                table: "shipments");

            // migrationBuilder.AddColumn<DateTime>(
            //     name: "updated_at",
            //     table: "saved_addresses",
            //     type: "timestamp with time zone",
            //     nullable: false,
            //     defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
