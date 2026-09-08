using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Orders",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Orders\" SET \"Currency\" = 'EUR' WHERE \"Currency\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Orders",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ProviderPaymentIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ExpectedAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.CheckConstraint("CK_PaymentTransactions_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_PaymentTransactions_ExpectedAmountMinor_NonNegative", "\"ExpectedAmountMinor\" >= 0");
                    table.CheckConstraint("CK_PaymentTransactions_Lifecycle", "(\"Status\" = 'Pending' AND \"PaidOn\" IS NULL AND \"FailedOn\" IS NULL AND \"CancelledOn\" IS NULL) OR (\"Status\" = 'Paid' AND \"PaidOn\" IS NOT NULL AND \"FailedOn\" IS NULL AND \"CancelledOn\" IS NULL) OR (\"Status\" = 'Failed' AND \"PaidOn\" IS NULL AND \"FailedOn\" IS NOT NULL AND \"CancelledOn\" IS NULL) OR (\"Status\" = 'Cancelled' AND \"PaidOn\" IS NULL AND \"FailedOn\" IS NULL AND \"CancelledOn\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentProviderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderCreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingOutcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReceivedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ProcessedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProviderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentProviderEvents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentProviderEvents_PaymentTransactions_PaymentTransactio~",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Currency",
                table: "Orders",
                sql: "\"Currency\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_TotalAmount_NonNegative",
                table: "Orders",
                sql: "\"TotalAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLines_LineTotal_NonNegative",
                table: "OrderLines",
                sql: "\"LineTotal\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLines_UnitPrice_NonNegative",
                table: "OrderLines",
                sql: "\"UnitPrice\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderEvents_OrderId",
                table: "PaymentProviderEvents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderEvents_PaymentTransactionId",
                table: "PaymentProviderEvents",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderEvents_ProcessingOutcome_ReceivedOn",
                table: "PaymentProviderEvents",
                columns: new[] { "ProcessingOutcome", "ReceivedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderEvents_Provider_ProviderEventId",
                table: "PaymentProviderEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_OrderId_Provider",
                table: "PaymentTransactions",
                columns: new[] { "OrderId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_ProviderPaymentIntentId",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "ProviderPaymentIntentId" },
                unique: true,
                filter: "\"ProviderPaymentIntentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_ProviderSessionId",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "ProviderSessionId" },
                unique: true,
                filter: "\"ProviderSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Status_UpdatedOn",
                table: "PaymentTransactions",
                columns: new[] { "Status", "UpdatedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentProviderEvents");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Currency",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_TotalAmount_NonNegative",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLines_LineTotal_NonNegative",
                table: "OrderLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLines_UnitPrice_NonNegative",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Orders");
        }
    }
}
