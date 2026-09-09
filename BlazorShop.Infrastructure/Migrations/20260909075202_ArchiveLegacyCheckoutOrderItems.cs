using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveLegacyCheckoutOrderItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A PostgreSQL table rename is atomic and retains every row, column value, null,
            // anomaly, and primary key without inventing Orders from incomplete legacy data.
            migrationBuilder.RenameTable(
                name: "CheckoutOrderItems",
                newName: "LegacyCheckoutOrderItemsArchive");

            migrationBuilder.Sql(
                """
                COMMENT ON TABLE "LegacyCheckoutOrderItemsArchive" IS
                'Operational archive of legacy checkout-history rows. Not an active order-history source.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback restores the original table name with the archived rows still intact.
            // It does not make older and newer application binaries rolling-compatible.
            migrationBuilder.RenameTable(
                name: "LegacyCheckoutOrderItemsArchive",
                newName: "CheckoutOrderItems");

            migrationBuilder.Sql("COMMENT ON TABLE \"CheckoutOrderItems\" IS NULL;");
        }
    }
}
