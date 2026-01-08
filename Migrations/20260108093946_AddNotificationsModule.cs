using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_type WHERE typname = 'NotificationSoundType'
    ) THEN
        CREATE TYPE "NotificationSoundType"
        AS ENUM ('DEFAULT','FIRE','EMERGENCY');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_type WHERE typname = 'TelemetrySource'
    ) THEN
        CREATE TYPE "TelemetrySource"
        AS ENUM ('gateway','beacon','safety_card');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_type WHERE typname = 'notification_action_type'
    ) THEN
        CREATE TYPE notification_action_type
        AS ENUM ('acknowledge','resolve');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_type WHERE typname = 'notification_priority'
    ) THEN
        CREATE TYPE notification_priority
        AS ENUM ('low','normal','high','critical');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_type WHERE typname = 'notification_state'
    ) THEN
        CREATE TYPE notification_state
        AS ENUM ('open','acknowledged','resolved');
    END IF;
END $$;
""");



            migrationBuilder.CreateTable(
                name: "NotificationCategories",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SnoozeReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnoozeReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTypes",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryId = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationTypes_NotificationCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "NotificationCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<short>(type: "smallint", nullable: false),
                    TypeId = table.Column<short>(type: "smallint", nullable: true),
                    TypeKey = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    Sound = table.Column<string>(type: "text", nullable: true),
                    ContentAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ViaDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TelemetryId = table.Column<long>(type: "bigint", nullable: true),
                    BatteryLevel = table.Column<short>(type: "smallint", nullable: true),
                    SignalStrength = table.Column<short>(type: "smallint", nullable: true),
                    SignalQuality = table.Column<string>(type: "text", nullable: true),
                    InitiatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatorDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnoozeReason = table.Column<string>(type: "text", nullable: true),
                    SnoozeNote = table.Column<string>(type: "text", nullable: true),
                    SnoozeUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "jsonb", nullable: true),
                    RawEvent = table.Column<string>(type: "jsonb", nullable: true),
                    EventTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Devices_InitiatorDeviceId",
                        column: x => x.InitiatorDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Devices_ViaDeviceId",
                        column: x => x.ViaDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_NotificationCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "NotificationCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_NotificationTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "NotificationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_InitiatorUserId",
                        column: x => x.InitiatorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationActions_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginDisplay = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationActions_ActorUserId",
                table: "NotificationActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationActions_NotificationId_CreatedAt",
                table: "NotificationActions",
                columns: new[] { "NotificationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationCategories_Key",
                table: "NotificationCategories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_NotificationId",
                table: "NotificationRecipients",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_NotificationId_RecipientUserId",
                table: "NotificationRecipients",
                columns: new[] { "NotificationId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_RecipientUserId",
                table: "NotificationRecipients",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CategoryId",
                table: "Notifications",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeviceId",
                table: "Notifications",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_InitiatorDeviceId",
                table: "Notifications",
                column: "InitiatorDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_InitiatorUserId_EventTimestamp",
                table: "Notifications",
                columns: new[] { "InitiatorUserId", "EventTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OwnerUserId_EventTimestamp",
                table: "Notifications",
                columns: new[] { "OwnerUserId", "EventTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_State",
                table: "Notifications",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TypeId",
                table: "Notifications",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ViaDeviceId",
                table: "Notifications",
                column: "ViaDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTypes_CategoryId",
                table: "NotificationTypes",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTypes_Key",
                table: "NotificationTypes",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SnoozeReasons_Key",
                table: "SnoozeReasons",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationActions");

            migrationBuilder.DropTable(
                name: "NotificationRecipients");

            migrationBuilder.DropTable(
                name: "SnoozeReasons");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationTypes");

            migrationBuilder.DropTable(
                name: "NotificationCategories");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:TelemetrySource", "gateway,beacon,safety_card")
                .OldAnnotation("Npgsql:Enum:notification_action_type", "acknowledge,resolve")
                .OldAnnotation("Npgsql:Enum:notification_priority", "low,normal,high,critical")
                .OldAnnotation("Npgsql:Enum:notification_state", "open,acknowledged,resolved");
        }
    }
}
