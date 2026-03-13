using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Enums;
using OcufiiAPI.Models;

namespace OcufiiAPI.Data
{
    public class OcufiiDbContext : DbContext
    {
        public OcufiiDbContext(DbContextOptions<OcufiiDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Tenant> Tenants { get; set; } = null!;
        public DbSet<UserSetting> UserSettings { get; set; } = null!;
        public DbSet<UserAssistSetting> UserAssistSettings { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<DeviceType> DeviceTypes { get; set; } = null!;
        public DbSet<Device> Devices { get; set; } = null!;
        public DbSet<DeviceCredential> DeviceCredentials { get; set; } = null!;
        public DbSet<DeviceTelemetry> DeviceTelemetry { get; set; } = null!;
        public DbSet<Feature> Features { get; set; } = null!;
        public DbSet<UserFeature> UserFeatures { get; set; } = null!;
        public DbSet<NotificationCategory> NotificationCategories { get; set; } = null!;
        public DbSet<NotificationType> NotificationTypes { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<NotificationRecipient> NotificationRecipients { get; set; } = null!;
        public DbSet<NotificationAction> NotificationActions { get; set; } = null!;
        public DbSet<SnoozeReason> SnoozeReasons { get; set; } = null!;

        public DbSet<DeviceToken> DeviceToken { get; set; } = null!;
        public DbSet<PlatformAdmin> PlatformAdmins { get; set; } = null!;
        public DbSet<Reseller> Resellers { get; set; } = null!;
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; } = null!;
        public DbSet<SafetyLink> SafetyLinks { get; set; } = null!;
        public DbSet<PlatformAdminFeature> PlatformAdminFeatures { get; set; }
        public DbSet<ResellerFeature> ResellerFeatures { get; set; }
        public DbSet<ResellerAllowedTenantFeature> ResellerAllowedTenantFeatures { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeviceToken>(entity =>
            {
                entity.ToTable("DeviceToken");

                entity.HasKey(e => e.DeviceTokenId);

                entity.Property(e => e.DeviceTokenValue)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.HasIndex(e => e.DeviceTokenValue)
                      .IsUnique();

                entity.HasOne<User>() 
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .HasConstraintName("FK_DeviceToken_Users_UserId");
            });

            modelBuilder.Entity<SnoozeReason>(entity =>
            {
                entity.ToTable("SnoozeReasons");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Label).IsRequired().HasMaxLength(200);
                entity.HasIndex(e => e.Key).IsUnique();
            });

            // Enums
            modelBuilder.HasPostgresEnum<NotificationState>();
            modelBuilder.HasPostgresEnum<NotificationActionType>();
            modelBuilder.HasPostgresEnum<NotificationPriority>();

            modelBuilder.Entity<NotificationCategory>(entity =>
            {
                entity.ToTable("NotificationCategories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Key).IsUnique();
            });

            modelBuilder.Entity<NotificationType>(entity =>
            {
                entity.ToTable("NotificationTypes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.HasOne(nt => nt.Category)
                      .WithMany(nc => nc.Types)
                      .HasForeignKey(nt => nt.CategoryId);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.Title).IsRequired();
                entity.Property(e => e.Location).HasColumnType("jsonb");
                entity.Property(e => e.RawEvent).HasColumnType("jsonb");
                entity.Property(e => e.EventTimestamp).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(n => n.OwnerUser)
                      .WithMany()
                      .HasForeignKey(n => n.OwnerUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(n => n.InitiatorUser)
                      .WithMany()
                      .HasForeignKey(n => n.InitiatorUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(n => n.Device)
                      .WithMany()
                      .HasForeignKey(n => n.DeviceId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(n => n.ViaDevice)
                      .WithMany()
                      .HasForeignKey(n => n.ViaDeviceId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(n => n.InitiatorDevice)
                      .WithMany()
                      .HasForeignKey(n => n.InitiatorDeviceId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(n => n.Category)
                      .WithMany()
                      .HasForeignKey(n => n.CategoryId);

                entity.HasOne(n => n.Type)
                      .WithMany()
                      .HasForeignKey(n => n.TypeId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(n => new { n.OwnerUserId, n.EventTimestamp });
                entity.HasIndex(n => new { n.InitiatorUserId, n.EventTimestamp });
                entity.HasIndex(n => n.State);
                entity.HasIndex(n => n.DeviceId);
            });

            modelBuilder.Entity<NotificationRecipient>(entity =>
            {
                entity.ToTable("NotificationRecipients");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.OriginDisplay).HasColumnType("jsonb").HasDefaultValue("{}");

                entity.HasOne(nr => nr.Notification)
                      .WithMany(n => n.Recipients)
                      .HasForeignKey(nr => nr.NotificationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(nr => nr.RecipientUser)
                      .WithMany()
                      .HasForeignKey(nr => nr.RecipientUserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(nr => nr.RecipientUserId);
                entity.HasIndex(nr => nr.NotificationId);
                entity.HasIndex(nr => new { nr.NotificationId, nr.RecipientUserId }).IsUnique();
            });

            modelBuilder.Entity<NotificationAction>(entity =>
            {
                entity.ToTable("NotificationActions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.Comment).IsRequired();

                entity.HasOne(na => na.Notification)
                      .WithMany(n => n.Actions)
                      .HasForeignKey(na => na.NotificationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(na => na.ActorUser)
                      .WithMany()
                      .HasForeignKey(na => na.ActorUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(na => new { na.NotificationId, na.CreatedAt });
            });

            modelBuilder.HasPostgresEnum("TelemetrySource", new[] { "gateway", "beacon", "safety_card" });
            modelBuilder.HasPostgresEnum("NotificationSoundType", new[] { "DEFAULT", "FIRE", "EMERGENCY" });

            modelBuilder.Entity<DeviceType>(entity =>
            {
                entity.ToTable("DeviceTypes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.Key).IsRequired();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.ConnectsToMqtt).HasDefaultValue(false);
                entity.Property(e => e.RequiresAuth).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => e.Key).IsUnique();
            });

            modelBuilder.Entity<Device>(entity =>
            {
                entity.ToTable("Devices");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.MacAddress).IsRequired();
                entity.Property(e => e.Attributes).HasColumnType("jsonb").HasDefaultValue("{}");
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => e.MacAddress).IsUnique();
                entity.HasOne(d => d.DeviceType)
                      .WithMany()
                      .HasForeignKey(d => d.DeviceTypeId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.User)
                      .WithMany(u => u.Devices)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<DeviceCredential>(entity =>
            {
                entity.ToTable("DeviceCredentials");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.MqttUsername).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => e.MqttUsername).IsUnique();
                entity.HasOne(d => d.Device)
                      .WithOne()
                      .HasForeignKey<DeviceCredential>(d => d.DeviceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DeviceTelemetry>(entity =>
            {
                entity.ToTable("DeviceTelemetry");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SourceType)
                      .HasConversion<string>();
                entity.Property(e => e.Payload).HasColumnType("jsonb").HasDefaultValue("{}");
                entity.Property(e => e.ReceivedAt).HasDefaultValueSql("now()");
                entity.HasOne(d => d.Device)
                      .WithMany()
                      .HasForeignKey(d => d.DeviceId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.Property(e => e.Token).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.HasOne(d => d.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserSetting>(entity =>
            {
                entity.ToTable("UserSettings");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.NotificationSound)
                      .HasConversion<string>()
                      .HasDefaultValue(NotificationSoundType.DEFAULT);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.HasOne(d => d.User)
                      .WithMany()
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserAssistSetting>(entity =>
            {
                entity.ToTable("UserAssistSettings");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.Config).HasColumnType("jsonb").HasDefaultValue("{}");
                entity.HasOne(d => d.User)
                      .WithMany()
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(e => e.ResellerId);
                entity.Property(e => e.ThemeConfig).HasColumnType("jsonb");
                entity.Property(e => e.CustomWorkflows).HasColumnType("jsonb");
                entity.Property(e => e.DateCreated).HasDefaultValueSql("now()");
                entity.Property(e => e.DateUpdated).HasDefaultValueSql("now()");
                entity.Property(e => e.AssignedResellerId)
                .IsRequired();
            });

            modelBuilder.Entity<Feature>(entity =>
            {
                entity.ToTable("Features");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.FeatureType).HasDefaultValue(FeatureType.Platform);
            });

            modelBuilder.Entity<UserFeature>(entity =>
            {
                entity.ToTable("UserFeatures");
                entity.HasKey(e => new { e.UserId, e.FeatureId });
                entity.Property(e => e.IsEnabled).HasDefaultValue(false);
                //entity.Property(e => e.Right).HasConversion<string>();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.HasOne(uf => uf.User)
                      .WithMany(u => u.UserFeatures)
                      .HasForeignKey(uf => uf.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(uf => uf.Feature)
                      .WithMany()
                      .HasForeignKey(uf => uf.FeatureId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.ParentId).HasColumnType("uuid");
                entity.HasOne(u => u.Parent)
                      .WithMany(u => u.Dependents)
                      .HasForeignKey(u => u.ParentId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(u => u.Role)
                      .WithMany(r => r.Users)
                      .HasForeignKey(u => u.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(u => u.Tenant)
                      .WithMany(t => t.Users)
                      .HasForeignKey(u => u.TenantId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PlatformAdmin>(entity =>
            {
                entity.ToTable("PlatformAdmins");
                entity.HasKey(e => e.AdminId);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Reseller>(entity =>
            {
                entity.ToTable("Resellers");
                entity.HasKey(e => e.ResellerId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasOne<PlatformAdmin>()
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // NEW: SubscriptionPlan
            modelBuilder.Entity<SubscriptionPlan>(entity =>
            {
                entity.ToTable("SubscriptionPlans");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.PlanType).IsRequired().HasConversion<string>();
                entity.Property(e => e.MaxActiveLinks).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.ExpiryDate).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.HasOne<User>()
                      .WithOne()
                      .HasForeignKey<SubscriptionPlan>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.UserId).IsUnique();  // Performance: unique per user
            });

            // NEW: SafetyLink
            modelBuilder.HasPostgresEnum<SafetyLinkStatus>();

            modelBuilder.Entity<SafetyLink>(entity =>
            {
                entity.ToTable("SafetyLinks");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SenderId).IsRequired();
                entity.Property(e => e.RecipientId).IsRequired();
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.AliasName).HasMaxLength(100);
                entity.Property(e => e.EnableLocation).HasDefaultValue(false);
                entity.Property(e => e.EnableSafety).HasDefaultValue(false);
                entity.Property(e => e.EnableSecurity).HasDefaultValue(false);
                entity.Property(e => e.Snooze).HasDefaultValue(false);
                entity.Property(e => e.OTP).HasMaxLength(6);  // 6-digit OTP
                entity.Property(e => e.OTPExpiry).IsRequired(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(e => e.SenderId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(e => e.RecipientId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.SenderId, e.RecipientId }).IsUnique();  // No duplicate links
                entity.HasIndex(e => e.Status);  // Performance for queries
            });

            modelBuilder.Entity<ResellerAllowedTenantFeature>(entity =>
            {
                entity.ToTable("ResellerAllowedTenantFeatures");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Reseller)
                      .WithMany()
                      .HasForeignKey(e => e.ResellerId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Feature)
                      .WithMany()
                      .HasForeignKey(e => e.FeatureId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ResellerId, e.FeatureId }).IsUnique();
            });

            modelBuilder.Entity<ResellerFeature>(entity =>
            {
                entity.ToTable("ResellerFeatures");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.IsEnabled)
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("NOW()");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("NOW()");

                entity.HasOne(e => e.Reseller)
                    .WithMany()
                    .HasForeignKey(e => e.ResellerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Feature)
                    .WithMany()
                    .HasForeignKey(e => e.FeatureId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ResellerId, e.FeatureId })
                    .IsUnique();
            });

            var dateTimeProperties = modelBuilder.Model.GetEntityTypes()
                .SelectMany(e => e.GetProperties())
                .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                .Where(p => p.Name.Contains("Date") || p.Name.Contains("Time") || p.Name.Contains("Timestamp") || p.Name.Contains("Created") || p.Name.Contains("Updated"));

            foreach (var property in dateTimeProperties)
            {
                property.SetColumnType("timestamp with time zone");
            }
        }
    }
}