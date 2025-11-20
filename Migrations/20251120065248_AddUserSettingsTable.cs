using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Users_UserId1",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ActiveShooter",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AutoLogout",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Distress",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Emergency",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Emergency911",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PersonalSafety",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Sound",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "PersonalSafetyUserName",
                table: "Settings",
                newName: "PersonalSafetyUsername");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId1",
                table: "Settings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TosVersion",
                table: "Settings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PersonalSafetyUsername",
                table: "Settings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "MovementVibration",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "MovementSound",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "BypassFocus",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AutoLogoutInterval",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 15,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssistSettings",
                table: "Settings",
                type: "jsonb",
                nullable: true,
                defaultValue: "{}");

            migrationBuilder.AddColumn<bool>(
                name: "AutoLogoutEnabled",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NotificationSound",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "DEFAULT");

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "Settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Users_UserId",
                table: "Settings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Users_UserId1",
                table: "Settings",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Users_UserId",
                table: "Settings");

            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Users_UserId1",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AssistSettings",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AutoLogoutEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "NotificationSound",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "PersonalSafetyUsername",
                table: "Settings",
                newName: "PersonalSafetyUserName");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId1",
                table: "Settings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TosVersion",
                table: "Settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PersonalSafetyUserName",
                table: "Settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "MovementVibration",
                table: "Settings",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "MovementSound",
                table: "Settings",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "BypassFocus",
                table: "Settings",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "AutoLogoutInterval",
                table: "Settings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 15);

            migrationBuilder.AddColumn<bool>(
                name: "ActiveShooter",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoLogout",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Distress",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Emergency",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Emergency911",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PersonalSafety",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Sound",
                table: "Settings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Users_UserId1",
                table: "Settings",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
