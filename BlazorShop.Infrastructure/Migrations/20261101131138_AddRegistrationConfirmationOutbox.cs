using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationConfirmationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationConfirmationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailSnapshot = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SecurityStampHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestWindowStartedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestsInWindow = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAttemptOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationConfirmationDeliveries", x => x.Id);
                    table.CheckConstraint("CK_ConfirmationDelivery_Counts", "\"AttemptCount\" >= 0 AND \"Generation\" >= 1 AND \"RequestsInWindow\" >= 1");
                    table.CheckConstraint("CK_ConfirmationDelivery_State", "\"State\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_RegistrationConfirmationDeliveries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationConfirmationDeliveries_State_LeaseExpiresOn",
                table: "RegistrationConfirmationDeliveries",
                columns: new[] { "State", "LeaseExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationConfirmationDeliveries_State_NextAttemptOn",
                table: "RegistrationConfirmationDeliveries",
                columns: new[] { "State", "NextAttemptOn" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationConfirmationDeliveries_UserId",
                table: "RegistrationConfirmationDeliveries",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never silently discard delivery/recovery evidence during schema rollback.
            migrationBuilder.Sql("""
                LOCK TABLE "RegistrationConfirmationDeliveries" IN ACCESS EXCLUSIVE MODE;
                DO $rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "RegistrationConfirmationDeliveries") THEN
                        RAISE EXCEPTION 'Registration confirmation outbox is not empty; preserve or drain its records before an approved schema rollback.';
                    END IF;
                END
                $rollback$;
                """);
            migrationBuilder.DropTable(
                name: "RegistrationConfirmationDeliveries");
        }
    }
}
