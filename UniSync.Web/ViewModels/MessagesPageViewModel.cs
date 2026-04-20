using System;
using System.Collections.Generic;

namespace UniSync.Web.ViewModels
{
    public class MessagesPageViewModel
    {
        public string CurrentUserId { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;
        public string CurrentUserRoleLabel { get; set; } = string.Empty;
        public string? ActiveContactId { get; set; }
        public string? ActiveContactName { get; set; }
        public string? EmptyStateMessage { get; set; }
        public List<MessageContactViewModel> Contacts { get; set; } = new();
        public List<MessageItemViewModel> Messages { get; set; } = new();
    }

    public class MessageContactViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public DateTime? LastMessageAt { get; set; }
        public bool HasUnreadMessages { get; set; }
    }

    public class MessageItemViewModel
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsMine { get; set; }
    }
}
