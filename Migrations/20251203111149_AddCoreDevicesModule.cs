using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreDevicesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:NotificationSoundType", "DEFAULT,FIRE,EMERGENCY")
                .Annotation("Npgsql:Enum:TelemetrySource", "gateway,beacon,safety_card");

            migrationBuilder.CreateTable(
                name: "DeviceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ConnectsToMqtt = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RequiresAuth = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsUserVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DeviceTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MacAddress = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Information = table.Column<string>(type: "text", nullable: true),
                    Attributes = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeviceTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_DeviceTypes_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalTable: "DeviceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Devices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DeviceCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MqttUsername = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCredentials_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTelemetry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    ViaDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatteryLevel = table.Column<short>(type: "smallint", nullable: true),
                    SignalStrength = table.Column<short>(type: "smallint", nullable: true),
                    SignalQuality = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeviceTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTelemetry_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceTelemetry_Devices_ViaDeviceId",
                        column: x => x.ViaDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCredentials_DeviceId",
                table: "DeviceCredentials",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCredentials_MqttUsername",
                table: "DeviceCredentials",
                column: "MqttUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceTypeId",
                table: "Devices",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IsEnabled_IsDeleted",
                table: "Devices",
                columns: new[] { "IsEnabled", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_MacAddress",
                table: "Devices",
                column: "MacAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTelemetry_DeviceId",
                table: "DeviceTelemetry",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTelemetry_ViaDeviceId",
                table: "DeviceTelemetry",
                column: "ViaDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTypes_Key",
                table: "DeviceTypes",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCredentials");

            migrationBuilder.DropTable(
                name: "DeviceTelemetry");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "DeviceTypes");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:NotificationSoundType", "DEFAULT,FIRE,EMERGENCY")
                .OldAnnotation("Npgsql:Enum:TelemetrySource", "gateway,beacon,safety_card");
        }
    }
}
