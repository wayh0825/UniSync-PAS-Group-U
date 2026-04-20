using System;

namespace UniSync.Web.ViewModels
{
    public class AuditLogItemViewModel
    {
        public DateTime TimestampUtc { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string IpAddress { get; set; } = "N/A";
        public string Details { get; set; } = string.Empty;
        public bool IsError { get; set; }
    }
}
