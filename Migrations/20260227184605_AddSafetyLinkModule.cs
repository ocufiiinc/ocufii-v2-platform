using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSafetyLinkModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Resellers_AssignedResellerId",
                table: "Tenants");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:NotificationSoundType", "DEFAULT,FIRE,EMERGENCY")
                .Annotation("Npgsql:Enum:TelemetrySource", "gateway,beacon,safety_card")
                .Annotation("Npgsql:Enum:notification_action_type", "acknowledge,resolve")
                .Annotation("Npgsql:Enum:notification_priority", "low,normal,high,critical")
                .Annotation("Npgsql:Enum:notification_state", "open,acknowledged,resolved")
                .Annotation("Npgsql:Enum:safety_link_status", "pending,accepted,rejected,block,inactive")
                .OldAnnotation("Npgsql:Enum:NotificationSoundType", "DEFAULT,FIRE,EMERGENCY")
                .OldAnnotation("Npgsql:Enum:TelemetrySource", "gateway,beacon,safety_card")
                .OldAnnotation("Npgsql:Enum:notification_action_type", "acknowledge,resolve")
                .OldAnnotation("Npgsql:Enum:notification_priority", "low,normal,high,critical")
                .OldAnnotation("Npgsql:Enum:notification_state", "open,acknowledged,resolved");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssignedResellerId",
                table: "Tenants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "SafetyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AliasName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EnableLocation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EnableSafety = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EnableSecurity = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Snooze = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SnoozeStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnoozeEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OTP = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    OTPExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyLinks_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SafetyLinks_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanType = table.Column<string>(type: "text", nullable: false),
                    MaxActiveLinks = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyLinks_RecipientId",
                table: "SafetyLinks",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyLinks_SenderId_RecipientId",
                table: "SafetyLinks",
                columns: new[] { "SenderId", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyLinks_Status",
                table: "SafetyLinks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_UserId",
                table: "SubscriptionPlans",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Resellers_AssignedResellerId",
                table: "Tenants",
                column: "AssignedResellerId",
                principalTable: "Resellers",
                principalColumn: "ResellerId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Resellers_AssignedResellerId",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "SafetyLinks");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:NotificationSoundType", "DEFAULT,FIRE,EMERGENCY")
                .Annotation("Npgsql:Enum:TelemetrySource", "gateway,beacon,safety_card")
                .Annotation("Npgsql:Enum:notification_action_type", "acknowledge,resolve")
                .Annotation("Npgsql:Enum:notification_priority", "low,normal,high,critical")
                .Annotation("Npgsql:Enum:notification_state", "open,acknowledged,resolved")
                .OldAnnotation("Npgsql:Enum:NotificationSoundType", "DEFAULT,FIRE,EMERGENCY")
                .OldAnnotation("Npgsql:Enum:TelemetrySource", "gateway,beacon,safety_card")
                .OldAnnotation("Npgsql:Enum:notification_action_type", "acknowledge,resolve")
                .OldAnnotation("Npgsql:Enum:notification_priority", "low,normal,high,critical")
                .OldAnnotation("Npgsql:Enum:notification_state", "open,acknowledged,resolved")
                .OldAnnotation("Npgsql:Enum:safety_link_status", "pending,accepted,rejected,block,inactive");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssignedResellerId",
                table: "Tenants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Resellers_AssignedResellerId",
                table: "Tenants",
                column: "AssignedResellerId",
                principalTable: "Resellers",
                principalColumn: "ResellerId");
        }
    }
}
