using System.Collections.Generic;
using UniSync.Core.Entities;

namespace UniSync.Web.ViewModels
{
    public class NotificationsPageViewModel
    {
        public List<NotificationItemViewModel> Notifications { get; set; } = new();
        public List<SubmissionGroupMember> PendingInvitations { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int TodayCount { get; set; }
        public string Filter { get; set; } = "all";
        public string Search { get; set; } = string.Empty;
    }

    public class NotificationItemViewModel
    {
        public int Id { get; set; }
        public string TypeLabel { get; set; } = string.Empty;
        public string Icon { get; set; } = "notifications";
        public string IconClass { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public string ActionLabel { get; set; } = "Open";
        public string TimeLabel { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }
}