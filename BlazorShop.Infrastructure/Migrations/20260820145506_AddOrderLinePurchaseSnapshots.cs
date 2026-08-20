using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLinePurchaseSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorSnapshot",
                table: "OrderLines",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTotal",
                table: "OrderLines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "OrderLines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SizeScaleSnapshot",
                table: "OrderLines",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeValueSnapshot",
                table: "OrderLines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkuSnapshot",
                table: "OrderLines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "OrderLines" AS order_line
                SET
                    "ProductNameSnapshot" = COALESCE(
                        (SELECT product."Name"
                         FROM "Products" AS product
                         WHERE product."Id" = order_line."ProductId"),
                        '[Unavailable product]'),
                    "SkuSnapshot" = (
                        SELECT variant."Sku"
                        FROM "ProductVariants" AS variant
                        WHERE variant."Id" = order_line."ProductVariantId"),
                    "SizeScaleSnapshot" = (
                        SELECT CASE variant."SizeScale"
                            WHEN 1 THEN 'ClothingAlpha'
                            WHEN 2 THEN 'ClothingNumericEU'
                            WHEN 10 THEN 'ShoesEU'
                            WHEN 11 THEN 'ShoesUS'
                            WHEN 12 THEN 'ShoesUK'
                            ELSE 'Unknown'
                        END
                        FROM "ProductVariants" AS variant
                        WHERE variant."Id" = order_line."ProductVariantId"),
                    "SizeValueSnapshot" = (
                        SELECT variant."SizeValue"
                        FROM "ProductVariants" AS variant
                        WHERE variant."Id" = order_line."ProductVariantId"),
                    "ColorSnapshot" = (
                        SELECT variant."Color"
                        FROM "ProductVariants" AS variant
                        WHERE variant."Id" = order_line."ProductVariantId"),
                    "LineTotal" = order_line."UnitPrice" * order_line."Quantity";

                ALTER TABLE "OrderLines"
                    ALTER COLUMN "ProductNameSnapshot" DROP DEFAULT,
                    ALTER COLUMN "LineTotal" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorSnapshot",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "LineTotal",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "SizeScaleSnapshot",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "SizeValueSnapshot",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "SkuSnapshot",
                table: "OrderLines");
        }
    }
}
