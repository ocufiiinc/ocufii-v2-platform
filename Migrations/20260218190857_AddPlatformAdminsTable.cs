using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAdminsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedResellerId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformAdmins",
                columns: table => new
                {
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAdmins", x => x.AdminId);
                });

            migrationBuilder.CreateTable(
                name: "Resellers",
                columns: table => new
                {
                    ResellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactName = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByAdminAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resellers", x => x.ResellerId);
                    table.ForeignKey(
                        name: "FK_Resellers_PlatformAdmins_CreatedByAdminAdminId",
                        column: x => x.CreatedByAdminAdminId,
                        principalTable: "PlatformAdmins",
                        principalColumn: "AdminId");
                    table.ForeignKey(
                        name: "FK_Resellers_PlatformAdmins_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "PlatformAdmins",
                        principalColumn: "AdminId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_AssignedResellerId",
                table: "Tenants",
                column: "AssignedResellerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdmins_Email",
                table: "PlatformAdmins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resellers_CreatedByAdminAdminId",
                table: "Resellers",
                column: "CreatedByAdminAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Resellers_CreatedByAdminId",
                table: "Resellers",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Resellers_Email",
                table: "Resellers",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Resellers_AssignedResellerId",
                table: "Tenants",
                column: "AssignedResellerId",
                principalTable: "Resellers",
                principalColumn: "ResellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Resellers_AssignedResellerId",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "Resellers");

            migrationBuilder.DropTable(
                name: "PlatformAdmins");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_AssignedResellerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AssignedResellerId",
                table: "Tenants");
        }
    }
}
