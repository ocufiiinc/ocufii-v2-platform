using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Models;

namespace OcufiiAPI.Data
{
    public class OcufiiDbContext : DbContext
    {
        public OcufiiDbContext(DbContextOptions<OcufiiDbContext> options) : base(options) { }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
        public DbSet<Invite> Invites => Set<Invite>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<Billing> Billings => Set<Billing>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Beacon> Beacons => Set<Beacon>();
        public DbSet<Gateway> Gateways => Set<Gateway>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<BeaconData> BeaconData => Set<BeaconData>();
        public DbSet<GatewayData> GatewayData => Set<GatewayData>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Configuration> Configurations => Set<Configuration>();
        public DbSet<CustomerSupportTicket> CustomerSupportTickets => Set<CustomerSupportTicket>();
        public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
        public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
        public DbSet<GatewayBeacon> GatewayBeacons => Set<GatewayBeacon>();
        public DbSet<PromoGroup> PromoGroups => Set<PromoGroup>();
        public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
        public DbSet<ResellerDevice> ResellerDevices => Set<ResellerDevice>();
        public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
        public DbSet<Setting> Settings => Set<Setting>();
        public DbSet<SnoozeSetting> SnoozeSettings => Set<SnoozeSetting>();
        public DbSet<TermOfService> TermOfServices => Set<TermOfService>();
        public DbSet<Thing> Things => Set<Thing>();
        public DbSet<UserGateway> UserGateways => Set<UserGateway>();
        public DbSet<UserNotify> UserNotifies => Set<UserNotify>();
        public DbSet<UserPurchase> UserPurchases => Set<UserPurchase>();
        public DbSet<WeeklySystemActivityReport> WeeklySystemActivityReports => Set<WeeklySystemActivityReport>();
        public DbSet<UserSetting> UserSettings { get; set; } = null!;
        public DbSet<UserAssistSetting> UserAssistSettings { get; set; } = null!;

        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.UserId);

                entity.Property(e => e.Token).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                // Define relationship
                entity.HasOne(d => d.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserSetting>(entity =>
            {
                entity.ToTable("UserSettings");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId).IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.MovementSound).HasDefaultValue(true);
                entity.Property(e => e.MovementVibration).HasDefaultValue(true);
                entity.Property(e => e.AutoLogoutEnabled).HasDefaultValue(false);
                entity.Property(e => e.AutoLogoutInterval).HasDefaultValue(15);
                entity.Property(e => e.BypassFocus).HasDefaultValue(false);

                entity.Property(e => e.NotificationSound)
                      .HasConversion<string>()
                      .HasDefaultValue(NotificationSoundType.DEFAULT);

                entity.Property(e => e.PersonalSafetyUsername)
                      .HasColumnName("personal_safety_username");

                entity.Property(e => e.TosId).HasColumnName("TosId");
                entity.Property(e => e.TosVersion).HasColumnName("TosVersion");
                entity.Property(e => e.TermsAcceptedAt).HasColumnName("TermsAcceptedAt");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.User)
                      .WithMany()
                      .HasForeignKey(d => d.UserId)
                      .HasPrincipalKey(u => u.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // UserAssistSettings — PERFECT MAPPING
            modelBuilder.Entity<UserAssistSetting>(entity =>
            {
                entity.ToTable("UserAssistSettings");
                entity.HasKey(e => e.UserId);

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.Config)
                      .HasColumnName("Config")
                      .HasColumnType("jsonb")
                      .HasDefaultValue("{}");

                entity.Property(e => e.PersonalSafetyUsername)
                      .HasColumnName("personal_safety_username");

                entity.HasOne(d => d.User)
                      .WithMany()
                      .HasForeignKey(d => d.UserId)
                      .HasPrincipalKey(u => u.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Setting>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.AssistSettings)
                      .HasColumnType("jsonb")
                      .HasDefaultValue("{}");
                entity.Property(e => e.NotificationSound)
                      .HasConversion<string>()
                      .HasDefaultValue(NotificationSoundType.DEFAULT);
                entity.Property(e => e.AutoLogoutInterval)
                      .HasDefaultValue(15);
            });

            modelBuilder.Entity<User>()
                .HasOne(u => u.Setting)
                .WithOne(s => s.User)
                .HasForeignKey<Setting>(s => s.UserId);

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.RoleId);

                entity.Property(r => r.RoleName)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(r => r.PermissionLevel)
                      .IsRequired()
                      .HasMaxLength(50)
                      .HasColumnName("permission_level");                
            });

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var fk in entity.GetForeignKeys())
                {
                    fk.PrincipalKey.IsPrimaryKey();
                }
            }

            ConfigureCompositeKeys(modelBuilder);
            ConfigureColumnTypes(modelBuilder);
            ConfigureRelationships(modelBuilder);
            ConfigureConstraintsAndDefaults(modelBuilder);
        }

        private void ConfigureCompositeKeys(ModelBuilder modelBuilder)
        {
            // 1. beacondata
            modelBuilder.Entity<BeaconData>()
                .HasKey(b => new { b.UserId, b.BeaconMac, b.DateUpdated });

            // 2. gatewaydata
            modelBuilder.Entity<GatewayData>()
                .HasKey(g => new { g.UserId, g.GatewayMac, g.DateUpdated });

            // 3. notifications
            modelBuilder.Entity<Notification>()
                .HasKey(n => new { n.UserId, n.NotificationId, n.NotificationTimestamp });

            // 4. auditlogs
            modelBuilder.Entity<AuditLog>()
                .HasKey(a => new { a.LogId, a.Timestamp });

            // 5. beacons
            modelBuilder.Entity<Beacon>()
                .HasKey(b => new { b.UserId, b.BeaconMac });

            // 6. gateways
            modelBuilder.Entity<Gateway>()
                .HasKey(g => new { g.UserId, g.GatewayMac });

            // 7. gatewaybeacons
            modelBuilder.Entity<GatewayBeacon>()
                .HasKey(gb => new { gb.GatewayUserId, gb.GatewayMac, gb.BeaconUserId, gb.BeaconMac });

            // 8. emailverification
            modelBuilder.Entity<EmailVerification>()
                .HasKey(ev => new { ev.UserId, ev.Token });

            // 9. snoozesettings
            modelBuilder.Entity<SnoozeSetting>()
                .HasKey(ss => new { ss.UserId, ss.BeaconMac });

            // 10. things
            modelBuilder.Entity<Thing>()
                .HasKey(t => new { t.UserId, t.GatewayMac });

            // 11. usergateways
            modelBuilder.Entity<UserGateway>()
                .HasKey(ug => new { ug.UserId, ug.GatewayMac });

            // 12. resellerdevices
            modelBuilder.Entity<ResellerDevice>()
                .HasKey(rd => new { rd.ResellerId, rd.Id });

            // 13. configurations
            modelBuilder.Entity<Configuration>()
                .HasKey(c => new { c.ConfigurationId, c.ModeType });

            // 14. scheduledreports
            modelBuilder.Entity<ScheduledReport>()
                .HasKey(sr => new { sr.ScheduledReportId, sr.ReportType });
        }

        private void ConfigureColumnTypes(ModelBuilder modelBuilder)
        {
            // === jsonb columns ===
            modelBuilder.Entity<Tenant>()
                .Property(t => t.ThemeConfig).HasColumnType("jsonb");
            modelBuilder.Entity<Tenant>()
                .Property(t => t.CustomWorkflows).HasColumnType("jsonb");

            modelBuilder.Entity<FeatureFlag>()
                .Property(f => f.Config).HasColumnType("jsonb");

            modelBuilder.Entity<Beacon>()
                .Property(b => b.ConfigurationDetail).HasColumnType("jsonb");

            modelBuilder.Entity<Gateway>()
                .Property(g => g.WifiSettings).HasColumnType("jsonb");
            modelBuilder.Entity<Gateway>()
                .Property(g => g.ConfigurationDetail).HasColumnType("jsonb");

            modelBuilder.Entity<CustomerSupportTicket>()
                .Property(c => c.Attachments).HasColumnType("jsonb");

            modelBuilder.Entity<Configuration>()
                .Property(c => c.ConfigurationData).HasColumnType("jsonb").IsRequired();

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Details).HasColumnType("jsonb");

            modelBuilder.Entity<GatewayData>()
                .Property(g => g.Data).HasColumnType("jsonb");

            // === timestamp with time zone ===
            var dateTimeProperties = modelBuilder.Model.GetEntityTypes()
                .SelectMany(e => e.GetProperties())
                .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                .Where(p => p.Name.Contains("Date") ||
                           p.Name.Contains("Time") ||
                           p.Name.Contains("Timestamp") ||
                           p.Name.Contains("Created") ||
                           p.Name.Contains("Updated"));

            foreach (var property in dateTimeProperties)
            {
                property.SetColumnType("timestamp with time zone");
            }
        }

        private void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            // User → Tenant
            modelBuilder.Entity<User>()
                .HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.SetNull);

            // User → Role
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Beacon → User
            modelBuilder.Entity<Beacon>()
                .HasOne(b => b.User)
                .WithMany(u => u.Beacons)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Gateway → User
            modelBuilder.Entity<Gateway>()
                .HasOne(g => g.User)
                .WithMany(u => u.Gateways)
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // BeaconData → Beacon
            modelBuilder.Entity<BeaconData>()
                .HasOne(bd => bd.Beacon)
                .WithMany(b => b.BeaconData)
                .HasForeignKey(bd => new { bd.UserId, bd.BeaconMac })
                .OnDelete(DeleteBehavior.Cascade);

            // GatewayData → Gateway
            modelBuilder.Entity<GatewayData>()
                .HasOne(gd => gd.Gateway)
                .WithMany(g => g.GatewayData)
                .HasForeignKey(gd => new { gd.UserId, gd.GatewayMac })
                .OnDelete(DeleteBehavior.Cascade);

            // Notification → User
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Add more as needed...
        }

        private void ConfigureConstraintsAndDefaults(ModelBuilder modelBuilder)
        {
            // Default values
            modelBuilder.Entity<Tenant>()
                .Property(t => t.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<Tenant>()
                .Property(t => t.DateUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Role>()
                .Property(r => r.RoleId).UseIdentityAlwaysColumn(); 
        }
    }
}