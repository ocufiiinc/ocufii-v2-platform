using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OcufiiAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_SnakeCase_Final : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    ConfigurationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConfigurationData = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => new { x.ConfigurationId, x.ModeType });
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    RoleName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RoleDescription = table.Column<string>(type: "text", nullable: true),
                    permission_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledReports",
                columns: table => new
                {
                    ScheduledReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateScheduled = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReports", x => new { x.ScheduledReportId, x.ReportType });
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: true),
                    IsEnable = table.Column<bool>(type: "boolean", nullable: false),
                    NoOfBeacons = table.Column<int>(type: "integer", nullable: true),
                    NoOfGateways = table.Column<int>(type: "integer", nullable: true),
                    PlanId = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    Sms = table.Column<bool>(type: "boolean", nullable: true),
                    SubscriptionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.SubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    ResellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DateUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ThemeConfig = table.Column<string>(type: "jsonb", nullable: false),
                    CustomWorkflows = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.ResellerId);
                });

            migrationBuilder.CreateTable(
                name: "WeeklySystemActivityReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklySystemActivityReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    Resource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                    table.ForeignKey(
                        name: "FK_Permissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromoGroups",
                columns: table => new
                {
                    PromoGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromoGroupName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: true),
                    NumberOfPromos = table.Column<int>(type: "integer", nullable: true),
                    SubscriptionId = table.Column<string>(type: "character varying(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoGroups", x => x.PromoGroupId);
                    table.ForeignKey(
                        name: "FK_PromoGroups_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "SubscriptionId");
                });

            migrationBuilder.CreateTable(
                name: "Billings",
                columns: table => new
                {
                    BillingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<string>(type: "character varying(100)", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoicePeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvoicePeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billings", x => x.BillingId);
                    table.ForeignKey(
                        name: "FK_Billings_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "SubscriptionId");
                    table.ForeignKey(
                        name: "FK_Billings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "ResellerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    FlagId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FlagName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Config = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.FlagId);
                    table.ForeignKey(
                        name: "FK_FeatureFlags_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "ResellerId");
                });

            migrationBuilder.CreateTable(
                name: "ResellerDevices",
                columns: table => new
                {
                    ResellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BeaconMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResellerDevices", x => new { x.ResellerId, x.Id });
                    table.ForeignKey(
                        name: "FK_ResellerDevices_Tenants_ResellerId",
                        column: x => x.ResellerId,
                        principalTable: "Tenants",
                        principalColumn: "ResellerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Company = table.Column<string>(type: "text", nullable: true),
                    DateSubmitted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsLockedFromTwoStep = table.Column<bool>(type: "boolean", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    RetryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccountHold = table.Column<bool>(type: "boolean", nullable: false),
                    GmtInfo = table.Column<string>(type: "text", nullable: true),
                    Imei = table.Column<string>(type: "text", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "ResellerId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    OsType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PromoStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionId = table.Column<string>(type: "character varying(100)", nullable: true),
                    PromoGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPromotional = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Code);
                    table.ForeignKey(
                        name: "FK_PromoCodes_PromoGroups_PromoGroupId",
                        column: x => x.PromoGroupId,
                        principalTable: "PromoGroups",
                        principalColumn: "PromoGroupId");
                    table.ForeignKey(
                        name: "FK_PromoCodes_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "SubscriptionId");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PdfUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_Invoices_Billings_BillingId",
                        column: x => x.BillingId,
                        principalTable: "Billings",
                        principalColumn: "BillingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => new { x.LogId, x.Timestamp });
                    table.ForeignKey(
                        name: "FK_AuditLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "ResellerId");
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Beacons",
                columns: table => new
                {
                    BeaconMac = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeaconName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BeaconLocation = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BeaconType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    ConfigurationDetail = table.Column<string>(type: "jsonb", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beacons", x => new { x.UserId, x.BeaconMac });
                    table.ForeignKey(
                        name: "FK_Beacons_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerSupportTickets",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Attachments = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSupportTickets", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_CustomerSupportTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceTokenValue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MobileDevice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MobileOsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserId1 = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTokens", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_DeviceTokens_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailVerifications",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerifications", x => new { x.UserId, x.Token });
                    table.ForeignKey(
                        name: "FK_EmailVerifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Gateways",
                columns: table => new
                {
                    GatewayMac = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GatewayLocation = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    GatewayType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Firmware = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    QrCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    WifiSettings = table.Column<string>(type: "jsonb", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    ConfigurationDetail = table.Column<string>(type: "jsonb", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsSync = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gateways", x => new { x.UserId, x.GatewayMac });
                    table.ForeignKey(
                        name: "FK_Gateways_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    InviteId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitingUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    ExpiryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.InviteId);
                    table.ForeignKey(
                        name: "FK_Invites_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId");
                    table.ForeignKey(
                        name: "FK_Invites_Users_InvitingUserId",
                        column: x => x.InvitingUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NotificationCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NotificationReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NotificationStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: true),
                    Sound = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContentAvailable = table.Column<bool>(type: "boolean", nullable: true),
                    Acknowledge = table.Column<bool>(type: "boolean", nullable: true),
                    IsSnoozed = table.Column<bool>(type: "boolean", nullable: true),
                    Battery = table.Column<int>(type: "integer", nullable: true),
                    BeaconMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeviceMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DataUuid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => new { x.UserId, x.NotificationId, x.NotificationTimestamp });
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TermOfServices",
                columns: table => new
                {
                    TosId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TermOfServiceText = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermOfServices", x => x.TosId);
                    table.ForeignKey(
                        name: "FK_TermOfServices_Users_CreatedByUserUserId",
                        column: x => x.CreatedByUserUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserNotifies",
                columns: table => new
                {
                    UserNotifyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Otp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CustomMessage = table.Column<string>(type: "text", nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceiveAlert = table.Column<bool>(type: "boolean", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SenderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EnableLocation = table.Column<bool>(type: "boolean", nullable: true),
                    EnableRecipient = table.Column<bool>(type: "boolean", nullable: true),
                    MobileDevice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SnoozeStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnoozeEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifies", x => x.UserNotifyId);
                    table.ForeignKey(
                        name: "FK_UserNotifies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserPurchases",
                columns: table => new
                {
                    PurchaseToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OsType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPurchases", x => x.PurchaseToken);
                    table.ForeignKey(
                        name: "FK_UserPurchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    SubscriptionUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OsType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoRenewEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    IsCanceled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SubscriptionPlanType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PromoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RegionCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AcknowledgementState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LatestOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LinkedPurchaseToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AppAppleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BundleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BundleVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeviceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExpiresDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationSubType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OriginalPurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.SubscriptionUserId);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "SubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeaconData",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeaconMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Battery = table.Column<int>(type: "integer", nullable: true),
                    BeaconValue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SignalStrength = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeaconData", x => new { x.UserId, x.BeaconMac, x.DateUpdated });
                    table.ForeignKey(
                        name: "FK_BeaconData_Beacons_UserId_BeaconMac",
                        columns: x => new { x.UserId, x.BeaconMac },
                        principalTable: "Beacons",
                        principalColumns: new[] { "UserId", "BeaconMac" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BeaconData_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SnoozeSettings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeaconMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SnoozeDuration = table.Column<int>(type: "integer", nullable: true),
                    SnoozeTimestampStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnoozeTimestampEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BeaconUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeaconMac1 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnoozeSettings", x => new { x.UserId, x.BeaconMac });
                    table.ForeignKey(
                        name: "FK_SnoozeSettings_Beacons_BeaconUserId_BeaconMac1",
                        columns: x => new { x.BeaconUserId, x.BeaconMac1 },
                        principalTable: "Beacons",
                        principalColumns: new[] { "UserId", "BeaconMac" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SnoozeSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatewayBeacons",
                columns: table => new
                {
                    GatewayUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BeaconUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeaconMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateAssociated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GatewayUserId1 = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac1 = table.Column<string>(type: "text", nullable: false),
                    BeaconUserId1 = table.Column<Guid>(type: "uuid", nullable: false),
                    BeaconMac1 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayBeacons", x => new { x.GatewayUserId, x.GatewayMac, x.BeaconUserId, x.BeaconMac });
                    table.ForeignKey(
                        name: "FK_GatewayBeacons_Beacons_BeaconUserId1_BeaconMac1",
                        columns: x => new { x.BeaconUserId1, x.BeaconMac1 },
                        principalTable: "Beacons",
                        principalColumns: new[] { "UserId", "BeaconMac" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatewayBeacons_Gateways_GatewayUserId1_GatewayMac1",
                        columns: x => new { x.GatewayUserId1, x.GatewayMac1 },
                        principalTable: "Gateways",
                        principalColumns: new[] { "UserId", "GatewayMac" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatewayData",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: true),
                    GatewayStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayData", x => new { x.UserId, x.GatewayMac, x.DateUpdated });
                    table.ForeignKey(
                        name: "FK_GatewayData_Gateways_UserId_GatewayMac",
                        columns: x => new { x.UserId, x.GatewayMac },
                        principalTable: "Gateways",
                        principalColumns: new[] { "UserId", "GatewayMac" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatewayData_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Things",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Certificate = table.Column<string>(type: "text", nullable: true),
                    CertificateArn = table.Column<string>(type: "text", nullable: true),
                    CertificateId = table.Column<string>(type: "text", nullable: true),
                    PrivateKey = table.Column<string>(type: "text", nullable: true),
                    PublicKey = table.Column<string>(type: "text", nullable: true),
                    GatewayUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac1 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Things", x => new { x.UserId, x.GatewayMac });
                    table.ForeignKey(
                        name: "FK_Things_Gateways_GatewayUserId_GatewayMac1",
                        columns: x => new { x.GatewayUserId, x.GatewayMac1 },
                        principalTable: "Gateways",
                        principalColumns: new[] { "UserId", "GatewayMac" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Things_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserGateways",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateAssociated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GatewayUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayMac1 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGateways", x => new { x.UserId, x.GatewayMac });
                    table.ForeignKey(
                        name: "FK_UserGateways_Gateways_GatewayUserId_GatewayMac1",
                        columns: x => new { x.GatewayUserId, x.GatewayMac1 },
                        principalTable: "Gateways",
                        principalColumns: new[] { "UserId", "GatewayMac" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGateways_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveShooter = table.Column<bool>(type: "boolean", nullable: true),
                    AutoLogout = table.Column<bool>(type: "boolean", nullable: true),
                    AutoLogoutInterval = table.Column<int>(type: "integer", nullable: true),
                    BypassFocus = table.Column<bool>(type: "boolean", nullable: true),
                    Distress = table.Column<bool>(type: "boolean", nullable: true),
                    Emergency = table.Column<bool>(type: "boolean", nullable: true),
                    Emergency911 = table.Column<bool>(type: "boolean", nullable: true),
                    MovementSound = table.Column<bool>(type: "boolean", nullable: true),
                    MovementVibration = table.Column<bool>(type: "boolean", nullable: true),
                    PersonalSafety = table.Column<bool>(type: "boolean", nullable: true),
                    PersonalSafetyUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Sound = table.Column<bool>(type: "boolean", nullable: true),
                    TosId = table.Column<Guid>(type: "uuid", nullable: true),
                    TosVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserId1 = table.Column<Guid>(type: "uuid", nullable: false),
                    TermOfServiceTosId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Settings_TermOfServices_TermOfServiceTosId",
                        column: x => x.TermOfServiceTosId,
                        principalTable: "TermOfServices",
                        principalColumn: "TosId");
                    table.ForeignKey(
                        name: "FK_Settings_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Billings_SubscriptionId",
                table: "Billings",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Billings_TenantId",
                table: "Billings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSupportTickets_UserId",
                table: "CustomerSupportTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_UserId1",
                table: "DeviceTokens",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_TenantId",
                table: "FeatureFlags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayBeacons_BeaconUserId1_BeaconMac1",
                table: "GatewayBeacons",
                columns: new[] { "BeaconUserId1", "BeaconMac1" });

            migrationBuilder.CreateIndex(
                name: "IX_GatewayBeacons_GatewayUserId1_GatewayMac1",
                table: "GatewayBeacons",
                columns: new[] { "GatewayUserId1", "GatewayMac1" });

            migrationBuilder.CreateIndex(
                name: "IX_Invites_InvitingUserId",
                table: "Invites",
                column: "InvitingUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_RoleId",
                table: "Invites",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BillingId",
                table: "Invoices",
                column: "BillingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_RoleId",
                table: "Permissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_PromoGroupId",
                table: "PromoCodes",
                column: "PromoGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_SubscriptionId",
                table: "PromoCodes",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoGroups_SubscriptionId",
                table: "PromoGroups",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_TermOfServiceTosId",
                table: "Settings",
                column: "TermOfServiceTosId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_UserId1",
                table: "Settings",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_SnoozeSettings_BeaconUserId_BeaconMac1",
                table: "SnoozeSettings",
                columns: new[] { "BeaconUserId", "BeaconMac1" });

            migrationBuilder.CreateIndex(
                name: "IX_TermOfServices_CreatedByUserUserId",
                table: "TermOfServices",
                column: "CreatedByUserUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Things_GatewayUserId_GatewayMac1",
                table: "Things",
                columns: new[] { "GatewayUserId", "GatewayMac1" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGateways_GatewayUserId_GatewayMac1",
                table: "UserGateways",
                columns: new[] { "GatewayUserId", "GatewayMac1" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifies_UserId",
                table: "UserNotifies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPurchases_UserId",
                table: "UserPurchases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_SubscriptionId",
                table: "UserSubscriptions",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId",
                table: "UserSubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BeaconData");

            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "CustomerSupportTickets");

            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropTable(
                name: "EmailVerifications");

            migrationBuilder.DropTable(
                name: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "GatewayBeacons");

            migrationBuilder.DropTable(
                name: "GatewayData");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "PromoCodes");

            migrationBuilder.DropTable(
                name: "ResellerDevices");

            migrationBuilder.DropTable(
                name: "ScheduledReports");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "SnoozeSettings");

            migrationBuilder.DropTable(
                name: "Things");

            migrationBuilder.DropTable(
                name: "UserGateways");

            migrationBuilder.DropTable(
                name: "UserNotifies");

            migrationBuilder.DropTable(
                name: "UserPurchases");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "WeeklySystemActivityReports");

            migrationBuilder.DropTable(
                name: "Billings");

            migrationBuilder.DropTable(
                name: "PromoGroups");

            migrationBuilder.DropTable(
                name: "TermOfServices");

            migrationBuilder.DropTable(
                name: "Beacons");

            migrationBuilder.DropTable(
                name: "Gateways");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
