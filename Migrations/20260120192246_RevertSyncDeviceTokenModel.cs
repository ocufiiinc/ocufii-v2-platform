using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class RevertSyncDeviceTokenModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceToken_Users_UserId1",
                table: "DeviceToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeviceToken",
                table: "DeviceToken");

            migrationBuilder.DropIndex(
                name: "IX_DeviceToken_UserId1",
                table: "DeviceToken");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "DeviceToken");

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceTokenId",
                table: "DeviceToken",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeviceToken",
                table: "DeviceToken",
                column: "DeviceTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceToken_UserId",
                table: "DeviceToken",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DeviceToken",
                table: "DeviceToken");

            migrationBuilder.DropIndex(
                name: "IX_DeviceToken_UserId",
                table: "DeviceToken");

            migrationBuilder.DropColumn(
                name: "DeviceTokenId",
                table: "DeviceToken");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "DeviceToken",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeviceToken",
                table: "DeviceToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceToken_UserId1",
                table: "DeviceToken",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceToken_Users_UserId1",
                table: "DeviceToken",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId");
        }
    }
}
