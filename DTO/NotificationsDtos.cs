namespace OcufiiAPI.DTO
{
    public class AcknowledgeRequest
    {
        public string Comment { get; set; } = string.Empty;
    }

    public class BatchActionRequest
    {
        public List<BatchActionItem> Items { get; set; } = new();
    }

    public class BatchActionItem
    {
        public Guid NotificationId { get; set; }
        public string? Comment { get; set; }
    }
}
