using System.Text.Json;

namespace OcufiiAPI.Models
{
    // NotificationCategory.cs
    public class NotificationCategory
    {
        public short Id { get; set; }
        public string Key { get; set; } = string.Empty; // security, system, safety, snooze
        public string Name { get; set; } = string.Empty;

        public ICollection<NotificationType> Types { get; set; } = new List<NotificationType>();
    }

    // NotificationType.cs
    public class NotificationType
    {
        public short Id { get; set; }
        public string Key { get; set; } = string.Empty; // e.g., movement_detected
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        public short CategoryId { get; set; }
        public NotificationCategory Category { get; set; } = null!;
    }

    // NotificationState.cs
    public enum NotificationState
    {
        Open,
        Acknowledged,
        Resolved
    }

    // NotificationActionType.cs
    public enum NotificationActionType
    {
        Acknowledge,
        Resolve
    }

    // NotificationPriority.cs
    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    // Notification.cs
    public class Notification
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OwnerUserId { get; set; }
        public User OwnerUser { get; set; } = null!;

        public short CategoryId { get; set; }
        public NotificationCategory Category { get; set; } = null!;

        public short? TypeId { get; set; }
        public NotificationType? Type { get; set; }

        public string? TypeKey { get; set; }

        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        public NotificationState State { get; set; } = NotificationState.Open;

        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string? Sound { get; set; }
        public bool ContentAvailable { get; set; } = false;

        public Guid? DeviceId { get; set; }
        public Device? Device { get; set; }

        public Guid? ViaDeviceId { get; set; }
        public Device? ViaDevice { get; set; }

        public long? TelemetryId { get; set; }

        public short? BatteryLevel { get; set; }
        public short? SignalStrength { get; set; }
        public string? SignalQuality { get; set; }
        public Guid InitiatorUserId { get; set; }
        public User InitiatorUser { get; set; } = null!;

        public Guid? InitiatorDeviceId { get; set; }
        public Device? InitiatorDevice { get; set; }

        public string? SnoozeReason { get; set; }
        public string? SnoozeNote { get; set; }
        public DateTime? SnoozeUntil { get; set; }

        public string? Location { get; set; } = "{}";
        public string? RawEvent { get; set; } = "{}";

        public DateTime EventTimestamp { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<NotificationRecipient> Recipients { get; set; } = new List<NotificationRecipient>();
        public ICollection<NotificationAction> Actions { get; set; } = new List<NotificationAction>();
    }

    // NotificationRecipient.cs
    public class NotificationRecipient
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid NotificationId { get; set; }
        public Notification Notification { get; set; } = null!;

        public Guid RecipientUserId { get; set; }
        public User RecipientUser { get; set; } = null!;

        public string OriginDisplay { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // NotificationAction.cs
    public class NotificationAction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid NotificationId { get; set; }
        public Notification Notification { get; set; } = null!;

        public Guid ActorUserId { get; set; }
        public User ActorUser { get; set; } = null!;

        public NotificationActionType ActionType { get; set; }
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
