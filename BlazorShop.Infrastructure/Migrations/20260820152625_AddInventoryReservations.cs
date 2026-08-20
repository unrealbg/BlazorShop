using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ConsumedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservations", x => x.Id);
                    table.CheckConstraint("CK_InventoryReservations_Lifecycle", "(\"Status\" = 'Reserved' AND \"ConsumedOn\" IS NULL AND \"ReleasedOn\" IS NULL) OR (\"Status\" = 'Consumed' AND \"ConsumedOn\" IS NOT NULL AND \"ReleasedOn\" IS NULL) OR (\"Status\" = 'Released' AND \"ConsumedOn\" IS NULL AND \"ReleasedOn\" IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryReservations_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InventoryReservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE "Products"
                SET "Quantity" = 0
                WHERE "Quantity" < 0;

                UPDATE "ProductVariants"
                SET "Stock" = 0
                WHERE "Stock" < 0;

                UPDATE "Products" AS product
                SET "Quantity" = 0
                WHERE EXISTS (
                    SELECT 1
                    FROM "ProductVariants" AS variant
                    WHERE variant."ProductId" = product."Id");
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductVariants_Stock_NonNegative",
                table: "ProductVariants",
                sql: "\"Stock\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Quantity_NonNegative",
                table: "Products",
                sql: "\"Quantity\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId",
                table: "InventoryReservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId_ProductId",
                table: "InventoryReservations",
                columns: new[] { "OrderId", "ProductId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId_ProductVariantId",
                table: "InventoryReservations",
                columns: new[] { "OrderId", "ProductVariantId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductVariants_Stock_NonNegative",
                table: "ProductVariants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Quantity_NonNegative",
                table: "Products");
        }
    }
}
