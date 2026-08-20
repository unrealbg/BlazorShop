using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreserveCheckoutProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "OrderLines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "OrderLines");
        }
    }
}
