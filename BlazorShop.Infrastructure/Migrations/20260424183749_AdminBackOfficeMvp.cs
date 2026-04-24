using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminBackOfficeMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "Orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "RequirePasswordChange",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StoreSupportEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StoreSupportPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DefaultCulture = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MaintenanceModeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaintenanceMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AllowGuestCheckout = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultShippingStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutoConfirmPaidOrders = table.Column<bool>(type: "boolean", nullable: false),
                    OrderReferencePrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SmtpHost = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SmtpFromEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SmtpFromDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ActorUserId",
                table: "AdminAuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedOn",
                table: "AdminAuditLogs",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_EntityType_EntityId",
                table: "AdminAuditLogs",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RequirePasswordChange",
                table: "AspNetUsers");
        }
    }
}
