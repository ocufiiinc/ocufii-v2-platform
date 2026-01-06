using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Models;

namespace OcufiiAPI.Data
{
    public class OcufiiDbContext : DbContext
    {
        public OcufiiDbContext(DbContextOptions<OcufiiDbContext> options) : base(options) { }

        // USED TABLES ONLY
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresEnum("TelemetrySource", new[] { "gateway", "beacon", "safety_card" });
            modelBuilder.HasPostgresEnum("NotificationSoundType", new[] { "DEFAULT", "FIRE", "EMERGENCY" });

            // DeviceTypes
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

            // Devices
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

            // DeviceCredentials
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

            // DeviceTelemetry
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

            // RefreshTokens
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

            // UserSettings
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

            // UserAssistSettings
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

            // Tenants
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(e => e.ResellerId);
                entity.Property(e => e.ThemeConfig).HasColumnType("jsonb");
                entity.Property(e => e.CustomWorkflows).HasColumnType("jsonb");
                entity.Property(e => e.DateCreated).HasDefaultValueSql("now()");
                entity.Property(e => e.DateUpdated).HasDefaultValueSql("now()");
            });

            // Features
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
            });

            // UserFeatures
            modelBuilder.Entity<UserFeature>(entity =>
            {
                entity.ToTable("UserFeatures");
                entity.HasKey(e => new { e.UserId, e.FeatureId });
                entity.Property(e => e.IsEnabled).HasDefaultValue(false);
                entity.Property(e => e.Right).HasConversion<string>();
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

            // Users
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

            // Timestamp with time zone
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