using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddBooleanRightsToFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Right",
                table: "UserFeatures");

            migrationBuilder.DropColumn(
                name: "Right",
                table: "ResellerFeatures");

            migrationBuilder.DropColumn(
                name: "Right",
                table: "PlatformAdminFeatures");

            migrationBuilder.AddColumn<bool>(
                name: "CanCreate",
                table: "UserFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanDelete",
                table: "UserFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanEdit",
                table: "UserFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FullAccess",
                table: "UserFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnlyView",
                table: "UserFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreate",
                table: "ResellerFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanDelete",
                table: "ResellerFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanEdit",
                table: "ResellerFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FullAccess",
                table: "ResellerFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnlyView",
                table: "ResellerFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreate",
                table: "PlatformAdminFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanDelete",
                table: "PlatformAdminFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanEdit",
                table: "PlatformAdminFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FullAccess",
                table: "PlatformAdminFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnlyView",
                table: "PlatformAdminFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanCreate",
                table: "UserFeatures");

            migrationBuilder.DropColumn(
                name: "CanDelete",
                table: "UserFeatures");

            migrationBuilder.DropColumn(
                name: "CanEdit",
                table: "UserFeatures");

            migrationBuilder.DropColumn(
                name: "FullAccess",
                table: "UserFeatures");

            migrationBuilder.DropColumn(
                name: "OnlyView",
                table: "UserFeatures");

            migrationBuilder.DropColumn(
                name: "CanCreate",
                table: "ResellerFeatures");

            migrationBuilder.DropColumn(
                name: "CanDelete",
                table: "ResellerFeatures");

            migrationBuilder.DropColumn(
                name: "CanEdit",
                table: "ResellerFeatures");

            migrationBuilder.DropColumn(
                name: "FullAccess",
                table: "ResellerFeatures");

            migrationBuilder.DropColumn(
                name: "OnlyView",
                table: "ResellerFeatures");

            migrationBuilder.DropColumn(
                name: "CanCreate",
                table: "PlatformAdminFeatures");

            migrationBuilder.DropColumn(
                name: "CanDelete",
                table: "PlatformAdminFeatures");

            migrationBuilder.DropColumn(
                name: "CanEdit",
                table: "PlatformAdminFeatures");

            migrationBuilder.DropColumn(
                name: "FullAccess",
                table: "PlatformAdminFeatures");

            migrationBuilder.DropColumn(
                name: "OnlyView",
                table: "PlatformAdminFeatures");

            migrationBuilder.AddColumn<string>(
                name: "Right",
                table: "UserFeatures",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Right",
                table: "ResellerFeatures",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "Right",
                table: "PlatformAdminFeatures",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
