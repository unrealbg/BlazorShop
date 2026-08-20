using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "jsonb", nullable: true),
                    OutcomeVersion = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutIdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutIdempotencyRecords_OrderId",
                table: "CheckoutIdempotencyRecords",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutIdempotencyRecords_State_ExpiresOn",
                table: "CheckoutIdempotencyRecords",
                columns: new[] { "State", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutIdempotencyRecords_State_LeaseExpiresOn",
                table: "CheckoutIdempotencyRecords",
                columns: new[] { "State", "LeaseExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutIdempotencyRecords_UserId_IdempotencyKey",
                table: "CheckoutIdempotencyRecords",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutIdempotencyRecords");
        }
    }
}
