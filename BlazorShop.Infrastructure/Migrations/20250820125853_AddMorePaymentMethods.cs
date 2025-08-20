using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMorePaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("6f2c2a7e-9f9b-4a0d-9f7f-2a1b3c4d5e6f"), "Cash on Delivery" },
                    { new Guid("a3bb23e6-6a7c-4b7d-9c73-7d5f2bc2f7b1"), "PayPal" },
                    { new Guid("b2e5c1d4-7a9f-4d2c-8f1e-3a4b5c6d7e8f"), "Bank Transfer" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("6f2c2a7e-9f9b-4a0d-9f7f-2a1b3c4d5e6f"));

            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("a3bb23e6-6a7c-4b7d-9c73-7d5f2bc2f7b1"));

            migrationBuilder.DeleteData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("b2e5c1d4-7a9f-4d2c-8f1e-3a4b5c6d7e8f"));
        }
    }
}
