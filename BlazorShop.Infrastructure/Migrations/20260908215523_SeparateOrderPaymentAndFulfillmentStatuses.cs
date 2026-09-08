using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparateOrderPaymentAndFulfillmentStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShippingStatus",
                table: "Orders",
                newName: "LegacyShippingStatus");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Orders",
                newName: "LegacyStatus");

            migrationBuilder.AlterColumn<string>(
                name: "LegacyShippingStatus",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LegacyStatus",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressSnapshot",
                table: "Orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmailSnapshot",
                table: "Orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNameSnapshot",
                table: "Orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStatus",
                table: "Orders",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "OrderStatus",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddressSnapshot",
                table: "Orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE "Orders" AS o
                SET "PaymentMethod" = CASE
                        WHEN EXISTS (SELECT 1 FROM "PaymentTransactions" pt WHERE pt."OrderId" = o."Id" AND pt."Provider" = 'Stripe') THEN 'Stripe'
                        WHEN EXISTS (SELECT 1 FROM "CheckoutIdempotencyRecords" ci WHERE ci."OrderId" = o."Id" AND ci."PaymentMethodId" = '6f2c2a7e-9f9b-4a0d-9f7f-2a1b3c4d5e6f') THEN 'CashOnDelivery'
                        WHEN EXISTS (SELECT 1 FROM "CheckoutIdempotencyRecords" ci WHERE ci."OrderId" = o."Id" AND ci."PaymentMethodId" = 'b2e5c1d4-7a9f-4d2c-8f1e-3a4b5c6d7e8f') THEN 'BankTransfer'
                        ELSE 'Unknown'
                    END,
                    "PaymentStatus" = CASE
                        WHEN EXISTS (SELECT 1 FROM "PaymentTransactions" pt WHERE pt."OrderId" = o."Id" AND pt."Status" = 'Paid') THEN 'Paid'
                        WHEN EXISTS (SELECT 1 FROM "PaymentTransactions" pt WHERE pt."OrderId" = o."Id" AND pt."Status" = 'Failed') THEN 'Failed'
                        WHEN EXISTS (SELECT 1 FROM "PaymentTransactions" pt WHERE pt."OrderId" = o."Id" AND pt."Status" = 'Cancelled') THEN 'Cancelled'
                        WHEN o."LegacyStatus" = 'PaymentFailed' THEN 'Failed'
                        WHEN o."LegacyStatus" = 'Cancelled' THEN 'Cancelled'
                        ELSE 'Pending'
                    END,
                    "FulfillmentStatus" = CASE o."LegacyShippingStatus"
                        WHEN 'Shipped' THEN 'Shipped'
                        WHEN 'InTransit' THEN 'InTransit'
                        WHEN 'OutForDelivery' THEN 'OutForDelivery'
                        WHEN 'Delivered' THEN 'Delivered'
                        ELSE 'NotStarted'
                    END,
                    "SubtotalAmount" = o."TotalAmount";

                UPDATE "Orders" AS o
                SET "OrderStatus" = CASE
                    WHEN o."PaymentStatus" IN ('Failed', 'Cancelled') THEN 'Cancelled'
                    WHEN o."FulfillmentStatus" = 'Delivered' AND o."PaymentStatus" = 'Paid' THEN 'Completed'
                    WHEN o."PaymentStatus" = 'Paid' OR o."PaymentMethod" IN ('CashOnDelivery', 'BankTransfer') THEN 'Confirmed'
                    ELSE 'Pending'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_FulfillmentStatus_CreatedOn",
                table: "Orders",
                columns: new[] { "FulfillmentStatus", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderStatus_CreatedOn",
                table: "Orders",
                columns: new[] { "OrderStatus", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentStatus_CreatedOn",
                table: "Orders",
                columns: new[] { "PaymentStatus", "CreatedOn" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_DiscountAmount_NonNegative",
                table: "Orders",
                sql: "\"DiscountAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_FulfillmentStatus",
                table: "Orders",
                sql: "\"FulfillmentStatus\" IN ('NotStarted', 'Shipped', 'InTransit', 'OutForDelivery', 'Delivered')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_OrderStatus",
                table: "Orders",
                sql: "\"OrderStatus\" IN ('Pending', 'Confirmed', 'Completed', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PaymentMethod",
                table: "Orders",
                sql: "\"PaymentMethod\" IN ('Unknown', 'CashOnDelivery', 'BankTransfer', 'Stripe')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PaymentStatus",
                table: "Orders",
                sql: "\"PaymentStatus\" IN ('Pending', 'Paid', 'Failed', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_ShippingAmount_NonNegative",
                table: "Orders",
                sql: "\"ShippingAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_SubtotalAmount_NonNegative",
                table: "Orders",
                sql: "\"SubtotalAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_TaxAmount_NonNegative",
                table: "Orders",
                sql: "\"TaxAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_TotalAmount_Components",
                table: "Orders",
                sql: "\"TotalAmount\" = \"SubtotalAmount\" - \"DiscountAmount\" + \"ShippingAmount\" + \"TaxAmount\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_FulfillmentStatus_CreatedOn",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderStatus_CreatedOn",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentStatus_CreatedOn",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_DiscountAmount_NonNegative",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_FulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_OrderStatus",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PaymentMethod",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PaymentStatus",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_ShippingAmount_NonNegative",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_SubtotalAmount_NonNegative",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_TaxAmount_NonNegative",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_TotalAmount_Components",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillingAddressSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerEmailSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerNameSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingAddressSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubtotalAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "LegacyShippingStatus",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "PendingShipment",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LegacyStatus",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "LegacyShippingStatus",
                table: "Orders",
                newName: "ShippingStatus");

            migrationBuilder.RenameColumn(
                name: "LegacyStatus",
                table: "Orders",
                newName: "Status");
        }
    }
}
