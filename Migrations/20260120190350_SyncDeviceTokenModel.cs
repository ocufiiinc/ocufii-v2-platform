using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class SyncDeviceTokenModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceToken_Users_UserId1",
                table: "DeviceToken");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId1",
                table: "DeviceToken",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceToken_DeviceTokenValue",
                table: "DeviceToken",
                column: "DeviceTokenValue",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceToken_Users_UserId",
                table: "DeviceToken",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceToken_Users_UserId1",
                table: "DeviceToken",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceToken_Users_UserId",
                table: "DeviceToken");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceToken_Users_UserId1",
                table: "DeviceToken");

            migrationBuilder.DropIndex(
                name: "IX_DeviceToken_DeviceTokenValue",
                table: "DeviceToken");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId1",
                table: "DeviceToken",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceToken_Users_UserId1",
                table: "DeviceToken",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
