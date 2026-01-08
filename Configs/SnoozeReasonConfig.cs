namespace OcufiiAPI.Configs
{
    public class SnoozeReasonConfig
    {
        public List<SnoozeReasonItem> SnoozeReasons { get; set; } = new();
    }

    public class SnoozeReasonItem
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
